using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Services;

namespace eArchiveSystem.Application.Services
{
    public class IndexingService : IIndexingService
    {
        private readonly IDocumentRepository _documents;
        private readonly IMetadataRepository _metadata;
        private readonly IDocumentSearchRepository _searchRepository;

        public IndexingService(
            IDocumentRepository documents,
            IMetadataRepository metadata,
            IDocumentSearchRepository searchRepository)
        {
            _documents = documents;
            _metadata = metadata;
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

        public async Task<(List<SearchDocumentIndex>, long)> SearchAsync(SearchDocumentsDto dto, string? ownerUserId)
        {
            var (ids, total) = await _searchRepository.SearchAsync(dto, ownerUserId);

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
                    Department = doc.Department,
                    UserId = doc.UserId,
                    Category = doc.Metadata?.Category,
                    DocumentType = doc.Metadata?.DocumentType,
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

            return new SearchDocumentIndex
            {
                Id = document.Id,
                Title = document.Title,
                Content = string.IsNullOrWhiteSpace(document.Content)
                    ? document.Title
                    : document.Content,
                Department = document.Department,
                UserId = document.UserId,
                Category = metadata?.Category,
                DocumentType = metadata?.DocumentType,
                Tags = metadata?.Tags ?? new List<string>(),
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt
            };
        }
    }
}

