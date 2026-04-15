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
            var (hits, total) = await _searchRepository.SearchAsync(dto, scope);
            var ids = hits.Select(hit => hit.Id).ToList();

            if (ids == null || ids.Count == 0)
                return (new List<SearchDocumentIndex>(), 0);

            var documents = await _documents.GetByIdsAsync(ids);
            var lookup = documents.ToDictionary(d => d.Id);
            var hitLookup = hits.ToDictionary(hit => hit.Id);

            var results = ids
                .Where(id => lookup.ContainsKey(id))
                .Select(id => lookup[id])
                .Select(doc => new SearchDocumentIndex
                {
                    Id = doc.Id,
                    Title = doc.Title,
                    Content = doc.Content ?? doc.Title,
                    Description = doc.Metadata?.Description,
                    Snippet = hitLookup.TryGetValue(doc.Id, out var hit)
                        ? hit.Snippet ?? BuildFallbackSnippet(doc, dto.Query)
                        : BuildFallbackSnippet(doc, dto.Query),
                    InstitutionId = doc.InstitutionId ?? string.Empty,
                    DepartmentId = doc.DepartmentId ?? doc.Department ?? string.Empty,
                    Department = doc.Department,
                    UserId = doc.UserId,
                    IsSensitive = doc.IsSensitive,
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
                Description = metadata?.Description,
                InstitutionId = document.InstitutionId ?? owner?.InstitutionId ?? string.Empty,
                DepartmentId = document.DepartmentId ?? metadata?.DepartmentId ?? document.Department ?? owner?.DepartmentId ?? owner?.Department ?? string.Empty,
                Department = document.Department,
                UserId = document.UserId,
                IsSensitive = document.IsSensitive,
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

        private static string? BuildFallbackSnippet(Domain.Models.Document document, string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            var source = string.Join(
                " ",
                new[]
                {
                    document.Title,
                    document.Metadata?.Description,
                    document.Content
                }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (string.IsNullOrWhiteSpace(source))
                return null;

            var index = source.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return source.Length <= 180
                    ? source
                    : $"{source[..180]}...";
            }

            var start = Math.Max(0, index - 60);
            var length = Math.Min(source.Length - start, 180);
            var snippet = source.Substring(start, length).Trim();

            if (start > 0)
                snippet = $"...{snippet}";

            if (start + length < source.Length)
                snippet = $"{snippet}...";

            return snippet;
        }
    }
}

