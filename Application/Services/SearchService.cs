using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Services;

namespace eArchiveSystem.Application.Services
{
    public class SearchService : ISearchService
    {
        private readonly IIndexingService _indexingService;
        private readonly IAuditService _audit;

        public SearchService(
            IIndexingService indexingService,
            IAuditService audit)
        {
            _indexingService = indexingService;
            _audit = audit;
        }

        public async Task<object> SearchDocumentsAsync(
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

            var normalizedDto = new SearchDocumentsDto
            {
                Query = dto.Query,
                Category = dto.Category,
                Department = dto.Department,
                FromDate = dto.FromDate,
                ToDate = dto.ToDate,
                SortBy = dto.SortBy,
                Desc = dto.Desc,
                Page = dto.Page <= 0 ? 1 : dto.Page,
                PageSize = dto.PageSize <= 0 ? 10 : dto.PageSize
            };

            var searchResult = await _indexingService.SearchAsync(normalizedDto);

            return new
            {
                total = searchResult.Total,
                page = normalizedDto.Page,
                pageSize = normalizedDto.PageSize,
                data = searchResult.Results
            };
        }
    }
}