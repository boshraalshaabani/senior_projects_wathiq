using DocumentFormat.OpenXml.Office2010.Word;
using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Exceptions;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Security;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;

namespace eArchiveSystem.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documents;
        private readonly IFileHashCalculator _hashCalculator;
        private readonly IStorageService _storage;
        private readonly IUserRepository _users;
        private readonly IMetadataRepository _metadata;
        private readonly IAuditService _audit;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IIndexingService _indexing;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(
            IDocumentRepository documents,
            IFileHashCalculator hashCalculator,
            IStorageService storage,
            IUserRepository users,
            IMetadataRepository metadata,
            IAuditService audit,
            HttpClient httpClient,
            IConfiguration config,
            IIndexingService indexing,
            ILogger<DocumentService> logger)
        {
            _documents = documents;
            _hashCalculator = hashCalculator;
            _storage = storage;
            _users = users;
            _metadata = metadata;
            _audit = audit;
            _httpClient = httpClient;
            _config = config;
            _indexing = indexing;
            _logger = logger;
        }

        private bool CanAdd(string role)
        {
            return role == "User" || role == "Manager";
        }

        private bool CanDelete(Document doc, string userId, string role)
        {
            if (role == "Manager") return true;
            if (role == "User" && doc.UserId == userId) return true;
            return false;
        }

        private bool CanEdit(Document doc, string userId, string role)
        {
            if (role == "Manager") return true;
            if (role == "User" && doc.UserId == userId) return true;
            return false;
        }

        private bool CanView(Document doc, string userId, string role)
        {
            if (role == "Admin") return true;
            if (role == "Manager") return true;
            if (role == "User" && doc.UserId == userId) return true;

            return false;
        }

        private bool CanDownload(Document doc, string userId, string role)
        {
            return CanView(doc, userId, role);
        }

        public async Task<DocumentAddResult> AddDocumentAsync(string userId, AddDocumentDto dto)
        {
            var user = await _users.GetByIdAsync(userId)
                ?? throw new NotFoundException("User not found");

            if (!CanAdd(user.Role))
                throw new UnauthorizedActionException("You are not allowed to add documents");

            if (dto.File == null)
                throw new ValidationException("File is required");

            var fileHash = await _hashCalculator.ComputeHashAsync(dto.File);
            var existing = await _documents.GetByHashAsync(fileHash);

            if (existing != null)
            {
                return new DocumentAddResult
                {
                    IsDuplicate = true,
                    Document = existing,
                    Message = "File already exists"
                };
            }

            var savedPath = await _storage.SaveFileAsync(dto.File, "uploads");

            var doc = new Document
            {
                Title = string.IsNullOrWhiteSpace(dto.Title)
                    ? dto.File.FileName
                    : dto.Title,
                FileName = dto.File.FileName,
                FilePath = savedPath,
                ContentType = dto.File.ContentType,
                Size = dto.File.Length,
                FileHash = fileHash,
                Content = null,
                UserId = userId,
                Department = user.Department,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _documents.CreateAsync(doc);
            _logger.LogInformation("Document {DocumentId} was created and saved successfully", doc.Id);

            var fullSavedPath = Path.Combine(Directory.GetCurrentDirectory(), savedPath);
            if (!File.Exists(fullSavedPath))
                throw new NotFoundException("Document file not found");

            await TriggerOcrAsync(doc.Id, fullSavedPath);

            await _audit.LogAsync(
                userId,
                user.Role,
                "AddDocument",
                doc.Id,
                $"User {userId} uploaded document '{doc.Title}'");

            return new DocumentAddResult
            {
                IsDuplicate = false,
                Document = doc,
                Message = "Uploaded successfully"
            };
        }

        public async Task<Document> GetByIdAsync(string id)
        {
            var doc = await _documents.GetByIdAsync(id);
            if (doc == null)
                throw new NotFoundException("Document not found");

            var meta = await _metadata.GetByDocumentIdAsync(id);
            doc.Metadata = meta;

            return doc;
        }

        public async Task AttachMetadataAsync(string documentId)
        {
            var metadata = await _metadata.GetByDocumentIdAsync(documentId);
            if (metadata == null)
                return;

            await _documents.UpdateMetadataFieldsAsync(documentId, metadata);
        }

        public async Task<DocumentViewDto> ViewDocumentAsync(string id, string userId, string role, string? dept)
        {
            var doc = await _documents.GetByIdAsync(id);
            if (doc == null)
                throw new NotFoundException("Document not found");

            if (!CanView(doc, userId, role))
                throw new UnauthorizedActionException("You are not allowed to view this document");

            var owner = await _users.GetByIdAsync(doc.UserId);
            doc.OwnerName = owner?.Name;

            await _audit.LogAsync(
                userId,
                role,
                "ViewDocument",
                id,
                $"User {userId} viewed document {id}");

            return new DocumentViewDto
            {
                Id = doc.Id,
                Title = doc.Title,
                Department = doc.Department,
                OwnerName = owner?.Name,
                CreatedAt = doc.CreatedAt,
                Metadata = doc.Metadata,
                OwnerEmail = owner?.Email
            };
        }

        public async Task<(Stream FileStream, string FileName, string ContentType)> DownloadDocumentAsync(
            string id,
            string userId,
            string role,
            string? dept)
        {
            var doc = await _documents.GetByIdAsync(id);
            if (doc == null)
                throw new NotFoundException("Document not found");

            if (!CanDownload(doc, userId, role))
                throw new UnauthorizedActionException("You are not allowed to download this document");

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), doc.FilePath);

            if (!File.Exists(fullPath))
                throw new NotFoundException("Document file not found");

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);

            await _audit.LogAsync(
                userId,
                role,
                "DownloadDocument",
                id,
                $"User {userId} downloaded document {id}");

            return (stream, doc.FileName, doc.ContentType);
        }

        public async Task<DocumentUpdateResult> UpdateDocumentAsync(
            string documentId,
            UpdateDocumentDto dto,
            string userId,
            string role)
        {
            var doc = await _documents.GetByIdAsync(documentId);
            if (doc == null)
                throw new NotFoundException("Document not found");

            if (!CanEdit(doc, userId, role))
                throw new UnauthorizedActionException("You are not allowed to update this document");

            if (!string.IsNullOrWhiteSpace(dto.Title))
                doc.Title = dto.Title;

            var fileWasReplaced = false;

            if (dto.File != null)
            {
                var newHash = await _hashCalculator.ComputeHashAsync(dto.File);
                var existing = await _documents.GetByHashAsync(newHash);

                if (existing != null && existing.Id != documentId)
                    throw new ConflictException("Duplicate file detected");

                var oldPath = Path.Combine(Directory.GetCurrentDirectory(), doc.FilePath);
                if (File.Exists(oldPath))
                    File.Delete(oldPath);

                var newPath = await _storage.SaveFileAsync(dto.File, "uploads");

                doc.FileName = dto.File.FileName;
                doc.ContentType = dto.File.ContentType;
                doc.Size = dto.File.Length;
                doc.FileHash = newHash;
                doc.FilePath = newPath;
                doc.Content = null;
                fileWasReplaced = true;
            }

            doc.UpdatedAt = DateTime.UtcNow;
            await _documents.UpdateAsync(documentId, doc);
            _logger.LogInformation("Document {DocumentId} was updated successfully", documentId);

            if (fileWasReplaced)
            {
                await _indexing.RemoveDocumentAsync(documentId);
                await TriggerOcrAsync(doc.Id, Path.Combine(Directory.GetCurrentDirectory(), doc.FilePath));
            }
            else
            {
                await _documents.AttachMetadataAsync(documentId);
                await _indexing.SyncDocumentAsync(documentId);
            }

            await _audit.LogAsync(
                userId,
                role,
                "UpdateDocument",
                documentId,
                $"User {userId} updated document {documentId}");

            return new DocumentUpdateResult
            {
                Success = true,
                Document = doc,
                Message = "Updated successfully"
            };
        }

        public async Task DeleteDocumentAsync(string id, string userId, string role)
        {
            var doc = await _documents.GetByIdAsync(id);
            if (doc == null)
                throw new NotFoundException("Document not found");

            if (!CanDelete(doc, userId, role))
                throw new UnauthorizedActionException("You are not allowed to delete this document");

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), doc.FilePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            await _metadata.DeleteByDocumentIdAsync(doc.Id);

            var deleted = await _documents.DeleteAsync(doc.Id);
            if (!deleted)
                throw new NotFoundException("Document not found");

            await _indexing.RemoveDocumentAsync(doc.Id);

            await _audit.LogAsync(
                userId,
                role,
                "DeleteDocument",
                doc.Id,
                $"User {userId} deleted document '{doc.Title}'");
        }

        private async Task TriggerOcrAsync(string documentId, string filePath)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{_config["OcrService:BaseUrl"]}/api/ocr/process",
                    new
                    {
                        documentId,
                        filePath,
                        callbackUrl = $"{_config["App:BaseUrl"]}/api/ocr/callback?documentId={documentId}"
                    });

                if (response.IsSuccessStatusCode)
                    return;

                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "OCR request failed for document {DocumentId}. Status: {StatusCode}. Response: {ResponseBody}",
                    documentId,
                    response.StatusCode,
                    responseBody);

                _logger.LogWarning(
                    "OCR request failed for document {DocumentId}. The document remains saved and can be retried later.",
                    documentId);
                return;
            }
            catch (ApiException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "OCR request threw an exception for document {DocumentId}",
                    documentId);
                _logger.LogWarning(
                    "OCR service is unavailable for document {DocumentId}. The document remains saved and can be retried later.",
                    documentId);
            }
        }
    }
}
