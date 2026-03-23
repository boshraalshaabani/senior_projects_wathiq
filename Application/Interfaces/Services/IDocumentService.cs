using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Interfaces.Services
{
    public interface IDocumentService
    {
        //  Add document + trigger OCR
        Task<DocumentAddResult> AddDocumentAsync(string userId, AddDocumentDto dto);

        // Get document with metadata
        Task<Document?> GetByIdAsync(string id);

        // Delete document (file + metadata + record)
        Task<bool> DeleteDocumentAsync(string id, string userId, string role);

        // View Document with Authorization
        Task<DocumentViewDto?> ViewDocumentAsync(string documentId, string userId, string role, string? department);

        // Download Document with Authorization
        Task<(Stream FileStream, string FileName, string ContentType)?> DownloadDocumentAsync(
      string documentId,
           string userId,
          string role,
          string? department
        );

        // Update document (title / file)
        Task<DocumentUpdateResult> UpdateDocumentAsync(
        string documentId,
        UpdateDocumentDto dto,
        string userId,
        string role
);

        // Sync metadata into document
        Task AttachMetadataAsync(string documentId);


    }

}

