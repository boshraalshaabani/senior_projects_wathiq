using DocumentFormat.OpenXml.Office2010.Word;
using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Security;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SharpCompress.Common;
using System.Net.Http;
using System.Net.Http.Json;

namespace eArchiveSystem.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documents; 
        private readonly IFileHashCalculator _hashCalculator; //duplication
        private readonly IStorageService _storage; // File system
        private readonly IUserRepository _users;
        private readonly IMetadataRepository _metadata;
        private readonly IAuditService _audit; // logging
        private readonly HttpClient _httpClient; // OCR
        private readonly IConfiguration _config; // URLs
        private readonly IIndexingService _indexing;



        public DocumentService(IDocumentRepository documents,
                               IFileHashCalculator hashCalculator,
                               IStorageService storage,
                               IUserRepository users,
                               IMetadataRepository metadata,
                               IAuditService audit,
                               HttpClient httpClient,
                               IConfiguration config,
                               IIndexingService indexing)
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
        }
        // ==================================================
        //  ROLE PERMISSIONS (صلاحيات)
        // ==================================================

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

        // ==================================================
        // 📌 ADD DOCUMENT
        // ==================================================


        // Upload + Save file + OCR trigger 
        public async Task<DocumentAddResult> AddDocumentAsync(string userId, AddDocumentDto dto)
        {
            var user = await _users.GetByIdAsync(userId);

            if (!CanAdd(user.Role))
                throw new Exception("Not authorized to add documents");

            if (dto.File == null)
                throw new Exception("File is required");

            // Duplicate check
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

            // Save file
            var savedPath = await _storage.SaveFileAsync(dto.File, "uploads");

            var doc = new Document
            {
                Title = string.IsNullOrWhiteSpace(dto.Title)
                    ? dto.File.FileName
                    : dto.Title,

                FileName = dto.File.FileName,
                FilePath = savedPath,          // المسار
                ContentType = dto.File.ContentType,
                Size = dto.File.Length,
                FileHash = fileHash,

                Content = null,                // OCR later
                UserId = userId,
                Department = user.Department,

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                  
            };
          
            await _documents.CreateAsync(doc);

            

                var fullSavedPath = Path.Combine(Directory.GetCurrentDirectory(), savedPath);
            if (!System.IO.File.Exists(fullSavedPath))
                throw new Exception("Saved PDF not found");

            // Trigger OCR (async)
            try
            {
                await _httpClient.PostAsJsonAsync(
                    $"{_config["OcrService:BaseUrl"]}/api/ocr/process",
                    new
                    {
                        documentId = doc.Id,
                        filePath = fullSavedPath,
                        callbackUrl = $"{_config["App:BaseUrl"]}/api/ocr/callback?documentId={doc.Id}"
                    }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(" OCR CALL FAILED: " + ex.Message);
                // لا ترجع Error – الوثيقة انحفظت
            }

            // Audit log
            await _audit.LogAsync(
                userId,
                user.Role,
                "AddDocument",
                doc.Id,
                $"User {userId} uploaded document '{doc.Title}'"
            );

            return new DocumentAddResult
            {
                IsDuplicate = false,
                Document = doc,
                Message = "Uploaded successfully"
            };
        }
        // ===============================
        // GET DOCUMENT
        // ===============================


        // Get document + metadata
        public async Task<Document?> GetByIdAsync(string id)
        {
            var doc = await _documents.GetByIdAsync(id);
            if (doc == null)
                return null;

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


        // ==================================================
        // 📌 VIEW DOCUMENT
        // ==================================================


        // View with permission + audit
        public async Task<DocumentViewDto?> ViewDocumentAsync(string id, string userId, string role, string? dept)
        {
            var doc = await _documents.GetByIdAsync(id);
            if (doc == null)
                return null;

            if (!CanView(doc, userId, role))
                return null;

            var owner = await _users.GetByIdAsync(doc.UserId);
            doc.OwnerName = owner?.Name;   // اسم المستخدم
            await _audit.LogAsync(
                userId,
                role,
                "ViewDocument",
                id,
                $"User {userId} viewed document {id}"
            );
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



        // ==================================================
        // 📌 DOWNLOAD DOCUMENT
        // ==================================================



        // Download with permission

        public async Task<(Stream FileStream, string FileName, string ContentType)?>
            DownloadDocumentAsync(string id, string userId, string role, string? dept)
        {
            var doc = await _documents.GetByIdAsync(id);
            if (doc == null)
                return null;

            if (!CanDownload(doc, userId, role))
                return null;

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), doc.FilePath);

            if (!File.Exists(fullPath))
                return null;

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);

            await _audit.LogAsync(userId, role, "DownloadDocument", id,
                $"User {userId} downloaded document {id}");

            return (stream, doc.FileName, doc.ContentType);
        }

        // ==================================================
        // 📌 UPDATE DOCUMENT
        // ==================================================

        // Update metadata / file
        public async Task<DocumentUpdateResult> UpdateDocumentAsync(
            string documentId,
            UpdateDocumentDto dto,
            string userId,
            string role)
        {
            var doc = await _documents.GetByIdAsync(documentId);
            if (doc == null)
                return new DocumentUpdateResult { Success = false, Message = "Not found" };

            if (!CanEdit(doc, userId, role))
                return new DocumentUpdateResult { Success = false, Message = "Not authorized" };

            if (!string.IsNullOrWhiteSpace(dto.Title))
                doc.Title = dto.Title;

            var fileWasReplaced = false;

            if (dto.File != null)
            {
                var newHash = await _hashCalculator.ComputeHashAsync(dto.File);
                var existing = await _documents.GetByHashAsync(newHash);

                if (existing != null && existing.Id != documentId)
                {
                    return new DocumentUpdateResult
                    {
                        Success = false,
                        IsDuplicate = true,
                        ExistingDocumentId = existing.Id,
                        Message = "Duplicate file detected"
                    };
                }
                // Replace file
                var oldPath = Path.Combine(Directory.GetCurrentDirectory(), doc.FilePath);
                if (File.Exists(oldPath)) File.Delete(oldPath);

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

            if (fileWasReplaced)
            {
                await _indexing.RemoveDocumentAsync(documentId);

                try
                {
                    var response = await _httpClient.PostAsJsonAsync(
                        $"{_config["OcrService:BaseUrl"]}/api/ocr/process",
                        new
                        {
                            documentId = doc.Id,
                            filePath = Path.Combine(Directory.GetCurrentDirectory(), doc.FilePath),
                            callbackUrl = $"{_config["App:BaseUrl"]}/api/ocr/callback?documentId={doc.Id}"
                        }
                    );
                    Console.WriteLine("OCR STATUS: " + response.StatusCode);

                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("OCR RESPONSE: " + content);

                }
                catch (Exception ex)
                {
                    Console.WriteLine(" OCR CALL FAILED AFTER UPDATE: " + ex.Message);
                }
            }
            else
            {
                await _documents.AttachMetadataAsync(documentId);
              
                await _indexing.SyncDocumentAsync(documentId);
            }

            await _audit.LogAsync(userId, role, "UpdateDocument", documentId,
                $"User {userId} updated document {documentId}");

            return new DocumentUpdateResult
            {
                Success = true,
                Document = doc,
                Message = "Updated successfully"
            };
        }

        // ==================================================
        // 📌 DELETE DOCUMENT
        // ==================================================


        // Delete file + metadata + document
        public async Task<bool> DeleteDocumentAsync(string id, string userId, string role)
        {
            // 1️⃣ جلب الوثيقة
            var doc = await _documents.GetByIdAsync(id);
            if (doc == null)
                return false;

            // 2️⃣ التحقق من الصلاحيات
            if (!CanDelete(doc, userId, role))
                return false;

            // 3️⃣ حذف الملف من التخزين
            var fullPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                doc.FilePath
            );

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            // 4️⃣ حذف Metadata (مرة واحدة فقط)
            // بما أن metadata._id == document._id
            await _metadata.DeleteByDocumentIdAsync(doc.Id);

            // 5️⃣ حذف Document
            var deleted = await _documents.DeleteAsync(doc.Id);
            if (!deleted)
                return false;

            await _indexing.RemoveDocumentAsync(doc.Id);

            // 6️⃣ تسجيل العملية في Audit Log
            await _audit.LogAsync(
                userId,
                role,
                "DeleteDocument",
                doc.Id,
                $"User {userId} deleted document '{doc.Title}'"
            );

            return true;
        }


    }
}
