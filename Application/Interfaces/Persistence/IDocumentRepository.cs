using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Interfaces.Persistence
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(string id);
        Task<Document> GetByHashAsync(string fileHash);
        Task CreateAsync(Document document);
        Task<List<Document>> GetByUserAsync(string userId);
        Task UpdateAsync(string id, Document document);
        Task UpdateStatusAsync(string id, DocumentStatus status);
        Task<bool> DeleteAsync(string id);
        Task<List<Document>> GetAllAsync();
        Task<List<Document>> GetByIdsAsync(IReadOnlyCollection<string> ids);
        Task AttachMetadataAsync(string documentId);
        Task UpdateMetadataFieldsAsync(string documentId, Metadata metadata);
        Task UpdateContentAsync(string documentId, string content, string? department, string? departmentId);
    }
}
