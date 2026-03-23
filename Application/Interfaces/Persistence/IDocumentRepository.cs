using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Interfaces.Persistence
{
    public interface IDocumentRepository
    {
        // Get document by primary Id
        Task<Document?> GetByIdAsync(string id);

        // Get document by file hash (duplicate check)
        Task<Document> GetByHashAsync(string fileHash);

        // Create new document record
        Task CreateAsync(Document document);

        // Get documents owned by specific user
        Task<List<Document>> GetByUserAsync(string userId);

        // Full document replace (use with caution)
        Task UpdateAsync(string id, Document document);

        // Advanced search (text + metadata + role)
        Task<List<Document>> SearchAsync(
            SearchDocumentsDto dto,
            string userId,
            string role
        );

        // Delete document by Id
        Task<bool> DeleteAsync(string id);

        // Get all documents (admin / manager)
        Task<List<Document>> GetAllAsync();

        // Embed metadata into document
        Task AttachMetadataAsync(string documentId);

        // Update metadata fields only
        Task UpdateMetadataFieldsAsync(
            string documentId,
            Metadata metadata
        );

        // Update OCR content only
        Task UpdateContentAsync(
            string documentId,
            string content,
            string department
        );
    }
}
