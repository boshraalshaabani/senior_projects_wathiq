using eArchiveSystem.Application.DTOs;

namespace eArchiveSystem.Application.Interfaces.Persistence
{
    public interface IDocumentSearchRepository
    {
        Task IndexAsync(SearchDocumentIndex document);
        Task DeleteAsync(string documentId);
        Task<IReadOnlyList<string>> SearchAsync(SearchDocumentsDto dto, string? ownerUserId);
    }
}
