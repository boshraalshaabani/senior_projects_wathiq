using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Interfaces.Services
{
    public interface ISearchService
    {
        Task<List<Document>> SearchDocumentsAsync(SearchDocumentsDto dto, string userId, string role);

    }
}

