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
        private const string IndexAnalyzerName = "document_index_analyzer";
        private const string SearchAnalyzerName = "document_search_analyzer";

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
            await EnsureIndexExistsAsync(client);
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

        public async Task EnsureIndexExistsAsync()
        {
            using var client = CreateClient();
            await EnsureIndexExistsAsync(client);
        }

        public async Task RecreateIndexAsync()
        {
            using var client = CreateClient();
            using var deleteResponse = await client.DeleteAsync(_settings.IndexName);

            if (deleteResponse.StatusCode != HttpStatusCode.NotFound)
                deleteResponse.EnsureSuccessStatusCode();

            using var createRequest = CreateJsonRequest(
                HttpMethod.Put,
                _settings.IndexName,
                BuildIndexDefinition());

            using var createResponse = await client.SendAsync(createRequest);
            createResponse.EnsureSuccessStatusCode();
        }

        public async Task<(IReadOnlyList<string> Ids, long Total)> SearchAsync(SearchDocumentsDto dto, string? ownerUserId)
        {
            using var client = CreateClient();
            await EnsureIndexExistsAsync(client);
            using var request = CreateJsonRequest(
                HttpMethod.Post,
                $"{_settings.IndexName}/_search",
                BuildSearchPayload(dto, ownerUserId));

            using var response = await client.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return (Array.Empty<string>(), 0);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            if (!document.RootElement.TryGetProperty("hits", out var hitsNode) ||
                !hitsNode.TryGetProperty("hits", out var innerHits))
            {
                return (Array.Empty<string>(), 0);
            }

            long total = 0;
            if (hitsNode.TryGetProperty("total", out var totalNode) &&
                totalNode.TryGetProperty("value", out var totalValue))
            {
                total = totalValue.GetInt64();
            }

            var ids = innerHits
                .EnumerateArray()
                .Select(hit => hit.GetProperty("_id").GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToList();

            return (ids, total);
        }

        private async Task EnsureIndexExistsAsync(HttpClient client)
        {
            using var existsResponse = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, _settings.IndexName));

            if (existsResponse.StatusCode == HttpStatusCode.OK)
                return;

            if (existsResponse.StatusCode != HttpStatusCode.NotFound)
                existsResponse.EnsureSuccessStatusCode();

            using var createRequest = CreateJsonRequest(
                HttpMethod.Put,
                _settings.IndexName,
                BuildIndexDefinition());

            using var createResponse = await client.SendAsync(createRequest);

            if (createResponse.StatusCode == HttpStatusCode.BadRequest)
            {
                var content = await createResponse.Content.ReadAsStringAsync();

                if (content.Contains("resource_already_exists_exception", StringComparison.OrdinalIgnoreCase))
                    return;
            }

            createResponse.EnsureSuccessStatusCode();
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

        private static object BuildIndexDefinition()
        {
            return new
            {
                settings = new
                {
                    analysis = new
                    {
                        char_filter = new
                        {
                            arabic_char_mapping = new
                            {
                                type = "mapping",
                                mappings = new[]
                                {
                                    "\u0623 => \u0627",
                                    "\u0625 => \u0627",
                                    "\u0622 => \u0627",
                                    "\u0649 => \u064A",
                                    "\u0624 => \u0648",
                                    "\u0626 => \u064A",
                                    "\u0629 => \u0647"
                                }
                            }
                        },
                        filter = new
                        {
                            arabic_stop_custom = new
                            {
                                type = "stop",
                                stopwords = new[]
                                {
                                    "_arabic_",
                                    "\u0647\u0630\u0627",
                                    "\u0647\u0630\u0647",
                                    "\u0630\u0644\u0643",
                                    "\u062A\u0644\u0643",
                                    "\u0641\u064A",
                                    "\u0645\u0646",
                                    "\u0627\u0644\u0649",
                                    "\u0625\u0644\u0649",
                                    "\u0639\u0644\u0649",
                                    "\u0639\u0646",
                                    "\u062B\u0645",
                                    "\u0642\u062F"
                                }
                            },
                            english_stop_custom = new
                            {
                                type = "stop",
                                stopwords = "_english_"
                            },
                            english_stemmer = new
                            {
                                type = "stemmer",
                                language = "english"
                            },
                            arabic_stemmer = new
                            {
                                type = "stemmer",
                                language = "arabic"
                            }
                        },
                        analyzer = new
                        {
                            document_index_analyzer = new
                            {
                                type = "custom",
                                tokenizer = "standard",
                                char_filter = new[] { "arabic_char_mapping" },
                                filter = new[]
                                {
                                    "lowercase",
                                    "decimal_digit",
                                    "arabic_normalization",
                                    "arabic_stop_custom",
                                    "english_stop_custom",
                                    "arabic_stemmer",
                                    "english_stemmer"
                                }
                            },
                            document_search_analyzer = new
                            {
                                type = "custom",
                                tokenizer = "standard",
                                char_filter = new[] { "arabic_char_mapping" },
                                filter = new[]
                                {
                                    "lowercase",
                                    "decimal_digit",
                                    "arabic_normalization",
                                    "arabic_stop_custom",
                                    "english_stop_custom",
                                    "arabic_stemmer",
                                    "english_stemmer"
                                }
                            }
                        }
                    }
                },
                mappings = new
                {
                    properties = new
                    {
                        id = new { type = "keyword" },
                        title = new
                        {
                            type = "text",
                            analyzer = IndexAnalyzerName,
                            search_analyzer = SearchAnalyzerName,
                            fields = new
                            {
                                keyword = new
                                {
                                    type = "keyword",
                                    ignore_above = 256
                                }
                            }
                        },
                        content = new
                        {
                            type = "text",
                            analyzer = IndexAnalyzerName,
                            search_analyzer = SearchAnalyzerName
                        },
                        tags = new
                        {
                            type = "text",
                            analyzer = IndexAnalyzerName,
                            search_analyzer = SearchAnalyzerName
                        },
                        category = new { type = "keyword" },
                        documentType = new { type = "keyword" },
                        department = new { type = "keyword" },
                        userId = new { type = "keyword" },
                        createdAt = new { type = "date" },
                        updatedAt = new { type = "date" }
                    }
                }
            };
        }

        private static object BuildSearchPayload(SearchDocumentsDto dto, string? ownerUserId)
        {
            var must = new List<object>();
            var should = new List<object>();
            var filter = new List<object>();
            var sort = BuildSort(dto);
            var from = Math.Max(0, (dto.Page - 1) * dto.PageSize);

            if (!string.IsNullOrWhiteSpace(dto.Query))
            {
                must.Add(new
                {
                    multi_match = new
                    {
                        query = dto.Query,
                        fields = new[]
                        {
                            "title^5",
                            "tags^3",
                            "content^2"
                        },
                        type = "best_fields",
                        fuzziness = "AUTO"
                    }
                });

                should.Add(new
                {
                    match_phrase = new
                    {
                        title = new
                        {
                            query = dto.Query,
                            boost = 8
                        }
                    }
                });

                should.Add(new
                {
                    match_phrase = new
                    {
                        content = new
                        {
                            query = dto.Query,
                            slop = 2,
                            boost = 4
                        }
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

            var boolQuery = new
            {
                @bool = new
                {
                    must = must.Count > 0
                        ? must
                        : new List<object> { new { match_all = new { } } },
                    should,
                    filter
                }
            };

            var queryObject = new
            {
                function_score = new
                {
                    query = boolQuery,
                    boost_mode = "sum",
                    score_mode = "sum",
                    functions = new object[]
                    {
                        new
                        {
                            gauss = new
                            {
                                createdAt = new
                                {
                                    scale = "30d",
                                    decay = 0.7
                                }
                            },
                            weight = 1.5
                        }
                    }
                }
            };

            if (sort is null)
            {
                return new
                {
                    from,
                    size = dto.PageSize,
                    track_total_hits = true,
                    query = queryObject
                };
            }

            return new
            {
                from,
                size = dto.PageSize,
                track_total_hits = true,
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
                        new Dictionary<string, object>
                        {
                            ["title.keyword"] = new
                            {
                                unmapped_type = "keyword",
                                missing = "_last",
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
