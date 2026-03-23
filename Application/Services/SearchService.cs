using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Services
{
        public class SearchService : ISearchService
        {
            private readonly IDocumentRepository _repo;
            private readonly IAuditService _audit;


        public SearchService(IDocumentRepository repo,
            IAuditService audit)
            {
                _repo = repo;
            _audit = audit;
        }

            public async Task<List<Document>> SearchDocumentsAsync(
                SearchDocumentsDto dto,
                string userId,
                string role)
            {
            await _audit.LogAsync(
          userId,
          role,
            "SearchDocuments",
            null,
            $"Search Query: {dto.Query}"
);

            return await _repo.SearchAsync(dto, userId, role);
            }
        }

    }
