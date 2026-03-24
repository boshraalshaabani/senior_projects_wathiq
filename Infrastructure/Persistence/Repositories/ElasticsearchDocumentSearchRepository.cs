using System.Net;
using System.Text;
using System.Text.Json;
using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Infrastructure.Persistence.Data;
using Microsoft.Extensions.Options;

namespace eArchiveSystem.Infrastructure.Persistence.Repositories
{
    public class ElasticsearchDocumentSearchRepository : IDocumentSearchRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ElasticsearchSettings _settings;

        public ElasticsearchDocumentSearchRepository(
            IHttpClientFactory httpClientFactory,
            IOptions<ElasticsearchSettings> settings)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
        }

        public async Task IndexAsync(SearchDocumentIndex document)
        {
            using var client = CreateClient();
            using var request = CreateJsonRequest(
                HttpMethod.Put,
                $"{_settings.IndexName}/_doc/{document.Id}",
                document);

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteAsync(string documentId)
        {
            using var client = CreateClient();
            using var response = await client.DeleteAsync($"{_settings.IndexName}/_doc/{documentId}");

            if (response.StatusCode != HttpStatusCode.NotFound)
                response.EnsureSuccessStatusCode();
        }

        public async Task<IReadOnlyList<string>> SearchAsync(SearchDocumentsDto dto, string? ownerUserId)
        {
            using var client = CreateClient();
            using var request = CreateJsonRequest(
                HttpMethod.Post,
                $"{_settings.IndexName}/_search",
                BuildSearchPayload(dto, ownerUserId));

            using var response = await client.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return Array.Empty<string>();

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            if (!document.RootElement.TryGetProperty("hits", out var hitsNode) ||
                !hitsNode.TryGetProperty("hits", out var innerHits))
            {
                return Array.Empty<string>();
            }

            return innerHits
                .EnumerateArray()
                .Select(hit => hit.GetProperty("_id").GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToList();
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient("ElasticClient");

            client.BaseAddress = new Uri(AppendTrailingSlash(_settings.Url));

            var byteArray = Encoding.ASCII.GetBytes($"{_settings.Username}:{_settings.Password}");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(byteArray)
                );

            return client;
        }

        private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string relativeUrl, object payload)
        {
            return new HttpRequestMessage(method, relativeUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, JsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };
        }

        private static object BuildSearchPayload(SearchDocumentsDto dto, string? ownerUserId)
        {
            var must = new List<object>();
            var filter = new List<object>();
            var sort = BuildSort(dto);

            if (!string.IsNullOrWhiteSpace(dto.Query))
            {
                must.Add(new
                {
                    multi_match = new
                    {
                        query = dto.Query,
                        fields = new[]
        {
            "title",
            "content"
        },
                        fuzziness = "AUTO"
                    }
                }); 
            }

            if (!string.IsNullOrWhiteSpace(ownerUserId))
            {
                filter.Add(new { term = new { userId = ownerUserId } });
            }

            if (!string.IsNullOrWhiteSpace(dto.Category))
            {
                filter.Add(new { term = new { category = dto.Category } });
            }

            if (!string.IsNullOrWhiteSpace(dto.Department))
            {
                filter.Add(new { term = new { department = dto.Department } });
            }

            if (dto.FromDate.HasValue || dto.ToDate.HasValue)
            {
                filter.Add(new
                {
                    range = new
                    {
                        createdAt = new
                        {
                            gte = dto.FromDate?.ToString("O"),
                            lte = dto.ToDate?.ToString("O")
                        }
                    }
                });
            }

            var queryObject = new
            {
                @bool = new
                {
                    must = must.Count > 0
            ? must
            : new List<object> { new { match_all = new { } } },
                    filter
                }
            };

            if (sort is null)
            {
                return new
                {
                    size = 200,
                    query = queryObject
                };
            }

            return new
            {
                size = 200,
                sort,
                query = queryObject
            };

        }

        private static object[]? BuildSort(SearchDocumentsDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.SortBy))
            {
                return dto.SortBy switch
                {
                    "Title" => new object[]
                    {
                        new
                        {
                            title = new
                            {
                                order = dto.Desc ? "desc" : "asc"
                            }
                        }
                    },
                    "CreatedAt" => new object[]
                    {
                        new
                        {
                            createdAt = new
                            {
                                order = dto.Desc ? "desc" : "asc"
                            }
                        }
                    },
                    _ => null
                };
            }

            if (string.IsNullOrWhiteSpace(dto.Query))
            {
                return new object[]
                {
                    new
                    {
                        createdAt = new
                        {
                            order = "desc"
                        }
                    }
                };
            }

            return null;
        }

        private static string AppendTrailingSlash(string url)
        {
            return url.EndsWith("/", StringComparison.Ordinal) ? url : $"{url}/";
        }
    }
}
