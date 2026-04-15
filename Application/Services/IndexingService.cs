using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Services;

namespace eArchiveSystem.Application.Services
{
    public class IndexingService : IIndexingService
    {
        private readonly IDocumentRepository _documents;
        private readonly IMetadataRepository _metadata;
        private readonly IUserRepository _users;
        private readonly IDocumentSearchRepository _searchRepository;

        public IndexingService(
            IDocumentRepository documents,
            IMetadataRepository metadata,
            IUserRepository users,
            IDocumentSearchRepository searchRepository)
        {
            _documents = documents;
            _metadata = metadata;
            _users = users;
            _searchRepository = searchRepository;
        }

        public async Task SyncDocumentAsync(string documentId)
        {
            var document = await _documents.GetByIdAsync(documentId);
            if (document == null)
                return;

            var indexedDocument = await BuildSearchDocumentAsync(document);
            await _searchRepository.IndexAsync(indexedDocument);
        }

        public Task RemoveDocumentAsync(string documentId)
        {
            return _searchRepository.DeleteAsync(documentId);
        }

        public Task EnsureIndexReadyAsync()
        {
            return _searchRepository.EnsureIndexExistsAsync();
        }

        public async Task ReindexAllAsync(bool recreateIndex = false)
        {
            if (recreateIndex)
                await _searchRepository.RecreateIndexAsync();
            else
                await _searchRepository.EnsureIndexExistsAsync();

            var documents = await _documents.GetAllAsync();

            foreach (var document in documents)
            {
                var indexedDocument = await BuildSearchDocumentAsync(document);
                await _searchRepository.IndexAsync(indexedDocument);
            }
        }

        public async Task<(List<SearchDocumentIndex>, long)> SearchAsync(SearchDocumentsDto dto, SearchAccessScope scope)
        {
            var (ids, total) = await _searchRepository.SearchAsync(dto, scope);

            if (ids == null || ids.Count == 0)
                return (new List<SearchDocumentIndex>(), 0);

            var documents = await _documents.GetByIdsAsync(ids);
            var lookup = documents.ToDictionary(d => d.Id);

            var results = ids
                .Where(id => lookup.ContainsKey(id))
                .Select(id => lookup[id])
                .Select(doc => new SearchDocumentIndex
                {
                    Id = doc.Id,
                    Title = doc.Title,
                    Content = doc.Content ?? doc.Title,
                    InstitutionId = doc.InstitutionId ?? string.Empty,
                    DepartmentId = doc.DepartmentId ?? doc.Department ?? string.Empty,
                    Department = doc.Department,
                    UserId = doc.UserId,
                    Category = doc.Metadata?.Category,
                    DocumentType = doc.Metadata?.DocumentType,
                    IssuingEntity = doc.Metadata?.IssuingEntity,
                    ReferenceNumber = doc.Metadata?.ReferenceNumber,
                    Status = doc.Status,
                    Priority = doc.Priority,
                    Tags = doc.Metadata?.Tags ?? new List<string>(),
                    CreatedAt = doc.CreatedAt,
                    UpdatedAt = doc.UpdatedAt
                })
                .ToList();

            return (results, total);
        }

        private async Task<SearchDocumentIndex> BuildSearchDocumentAsync(Domain.Models.Document document)
        {
            var metadata = document.Metadata ?? await _metadata.GetByDocumentIdAsync(document.Id);
            var owner = await _users.GetByIdAsync(document.UserId);

            return new SearchDocumentIndex
            {
                Id = document.Id,
                Title = document.Title,
                Content = string.IsNullOrWhiteSpace(document.Content)
                    ? document.Title
                    : document.Content,
                InstitutionId = document.InstitutionId ?? owner?.InstitutionId ?? string.Empty,
                DepartmentId = document.DepartmentId ?? metadata?.DepartmentId ?? document.Department ?? owner?.DepartmentId ?? owner?.Department ?? string.Empty,
                Department = document.Department,
                UserId = document.UserId,
                Category = metadata?.Category,
                DocumentType = metadata?.DocumentType,
                IssuingEntity = metadata?.IssuingEntity,
                ReferenceNumber = metadata?.ReferenceNumber,
                Status = document.Status,
                Priority = document.Priority,
                Tags = metadata?.Tags ?? new List<string>(),
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt
            };
        }
    }
}

