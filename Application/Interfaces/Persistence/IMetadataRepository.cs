using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Interfaces.Persistence
{
    public interface IMetadataRepository
    {
        // 🔹 Create or Update (Id = DocumentId)
        Task UpsertAsync(Metadata metadata);

        // 🔹 Read
        Task<Metadata?> GetByDocumentIdAsync(string documentId);

        // 🔹 Delete
        Task<bool> DeleteByDocumentIdAsync(string documentId);
    }
}
