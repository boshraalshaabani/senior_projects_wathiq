using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Security;
using eArchiveSystem.Domain.Models;
using eArchiveSystem.TestHost.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace eArchiveSystem.PerformanceRunner;

[Trait("Layer", "Performance")]
[Trait("Area", "OCR+Search")]
public class PerformanceBaselineTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceBaselineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GeneratePerformanceBaselineReport()
    {
        var outputDirectory = ResolveOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        var options = new PerformanceBaselineOptions();

        using var factory = new TestWebApplicationFactory();
        using var anonymousClient = factory.CreateApiClient();

        var manager = new User
        {
            Id = "perf-manager-1",
            Name = "Performance Manager",
            Email = "perf-manager@example.com",
            Password = factory.HashPassword("Pass123!"),
            Role = ApplicationRoles.Manager,
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "Records"
        };

        factory.State.Users[manager.Id] = manager;
        SeedSearchCorpus(factory.State, manager, options.SearchDatasetSize);

        using var managerClient = factory.CreateAuthenticatedClient(manager);

        await WarmupAsync(factory, anonymousClient, managerClient, manager);

        var ocrCallbackSamples = await MeasureManyAsync(
            options.OcrIterations,
            iteration => MeasureOcrCallbackAsync(factory, anonymousClient, manager, iteration));

        var searchableAfterCallbackSamples = await MeasureManyAsync(
            options.SearchableAfterCallbackIterations,
            iteration => MeasureSearchableAfterCallbackAsync(factory, anonymousClient, managerClient, manager, iteration));

        var searchSamples = await MeasureManyAsync(
            options.SearchIterations,
            _ => MeasureSearchLatencyAsync(managerClient));

        var report = new PerformanceReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Environment = "In-memory ASP.NET Core host",
            SearchDatasetSize = options.SearchDatasetSize,
            OcrIterations = options.OcrIterations,
            SearchIterations = options.SearchIterations,
            SearchableAfterCallbackIterations = options.SearchableAfterCallbackIterations,
            Metrics =
            [
                BuildMetric("OCR callback persistence", "POST /api/ocr/callback", ocrCallbackSamples),
                BuildMetric("Callback to searchable", "OCR callback + first successful search hit", searchableAfterCallbackSamples),
                BuildMetric("Search latency", "POST /api/documents/search", searchSamples)
            ]
        };

        var jsonPath = Path.Combine(outputDirectory, "performance-summary.json");
        var markdownPath = Path.Combine(outputDirectory, "performance-summary.md");

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, jsonOptions));
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report));

        foreach (var metric in report.Metrics)
        {
            _output.WriteLine(
                $"{metric.Name}: avg {metric.AverageMs:N2} ms | p95 {metric.P95Ms:N2} ms | max {metric.MaxMs:N2} ms");
        }

        Assert.Equal(3, report.Metrics.Count);
        Assert.All(report.Metrics, metric =>
        {
            Assert.True(metric.Iterations > 0);
            Assert.True(metric.MinMs >= 0);
            Assert.True(metric.MaxMs >= metric.MinMs);
        });
        Assert.True(File.Exists(jsonPath));
        Assert.True(File.Exists(markdownPath));
    }

    private static string ResolveOutputDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("WATHIQ_PERFORMANCE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".github")))
            {
                return Path.Combine(current.FullName, "artifacts", "performance");
            }

            current = current.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "performance");
    }

    private static async Task WarmupAsync(
        TestWebApplicationFactory factory,
        HttpClient anonymousClient,
        HttpClient managerClient,
        User manager)
    {
        await MeasureOcrCallbackAsync(factory, anonymousClient, manager, -1);
        await MeasureSearchLatencyAsync(managerClient);
        await MeasureSearchableAfterCallbackAsync(factory, anonymousClient, managerClient, manager, -2);
    }

    private static void SeedSearchCorpus(IntegrationTestState state, User manager, int datasetSize)
    {
        for (var i = 0; i < datasetSize; i++)
        {
            var sameDepartment = i < datasetSize * 0.75;
            var departmentId = sameDepartment ? manager.DepartmentId! : "dept-b";
            var departmentName = sameDepartment ? manager.Department! : "Finance";
            var titlePrefix = sameDepartment ? "Archive Case" : "Finance Memo";

            var document = new Document
            {
                Id = $"perf-search-{i}",
                Title = $"{titlePrefix} {i:D3}",
                Content = sameDepartment
                    ? $"archive contract record {i:D3} searchable baseline"
                    : $"finance budget note {i:D3}",
                FilePath = $"uploads/perf-search-{i}.pdf",
                FileName = $"perf-search-{i}.pdf",
                ContentType = "application/pdf",
                FileHash = $"perf-search-hash-{i}",
                Size = 2048,
                UserId = sameDepartment ? $"employee-a-{i}" : $"employee-b-{i}",
                InstitutionId = "inst-a",
                DepartmentId = departmentId,
                Department = departmentName,
                Status = DocumentStatus.Published,
                Priority = i % 3 == 0 ? DocumentPriority.Important : DocumentPriority.Normal,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-i)
            };

            state.Documents[document.Id] = document;
            state.IndexedDocumentIds.Add(document.Id);
        }
    }

    private static async Task<double> MeasureOcrCallbackAsync(
        TestWebApplicationFactory factory,
        HttpClient client,
        User manager,
        int iteration)
    {
        var documentId = $"perf-ocr-{iteration}-{Guid.NewGuid():N}";
        factory.State.Documents[documentId] = new Document
        {
            Id = documentId,
            Title = $"OCR Perf {iteration}",
            FilePath = $"uploads/{documentId}.pdf",
            FileName = $"{documentId}.pdf",
            ContentType = "application/pdf",
            FileHash = $"hash-{documentId}",
            Size = 1024,
            UserId = manager.Id,
            InstitutionId = manager.InstitutionId,
            DepartmentId = manager.DepartmentId,
            Department = manager.Department,
            Status = DocumentStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var started = Stopwatch.GetTimestamp();

        var response = await client.PostAsJsonAsync(
            $"/api/ocr/callback?documentId={documentId}",
            new OcrCallbackDto
            {
                Text = "the archive contract record 123",
                RawText = "THE archive contract record 123",
                NormalizedText = "the archive contract record 123",
                Language = "eng",
                Pages = 1,
                Provider = "performance-baseline"
            });

        response.EnsureSuccessStatusCode();

        return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private static async Task<double> MeasureSearchableAfterCallbackAsync(
        TestWebApplicationFactory factory,
        HttpClient anonymousClient,
        HttpClient managerClient,
        User manager,
        int iteration)
    {
        var marker = $"callback-searchable-{iteration}-{Guid.NewGuid():N}";
        factory.State.Documents[marker] = new Document
        {
            Id = marker,
            Title = marker,
            FilePath = $"uploads/{marker}.pdf",
            FileName = $"{marker}.pdf",
            ContentType = "application/pdf",
            FileHash = $"hash-{marker}",
            Size = 1024,
            UserId = manager.Id,
            InstitutionId = manager.InstitutionId,
            DepartmentId = manager.DepartmentId,
            Department = manager.Department,
            Status = DocumentStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var started = Stopwatch.GetTimestamp();

        var callbackResponse = await anonymousClient.PostAsJsonAsync(
            $"/api/ocr/callback?documentId={marker}",
            new OcrCallbackDto
            {
                Text = marker,
                RawText = marker,
                NormalizedText = marker,
                Language = "eng",
                Pages = 1,
                Provider = "performance-baseline"
            });

        callbackResponse.EnsureSuccessStatusCode();

        for (var attempts = 0; attempts < 5; attempts++)
        {
            var searchResponse = await managerClient.PostAsJsonAsync("/api/documents/search", new SearchDocumentsDto
            {
                Query = marker,
                Page = 1,
                PageSize = 5
            });

            searchResponse.EnsureSuccessStatusCode();

            using var payload = JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync());
            var items = payload.RootElement.GetProperty("data");

            if (items.EnumerateArray().Any(item => item.GetProperty("id").GetString() == marker))
            {
                return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }

            await Task.Delay(10);
        }

        throw new InvalidOperationException($"Document {marker} did not become searchable during the baseline run.");
    }

    private static async Task<double> MeasureSearchLatencyAsync(HttpClient managerClient)
    {
        var started = Stopwatch.GetTimestamp();

        var response = await managerClient.PostAsJsonAsync("/api/documents/search", new SearchDocumentsDto
        {
            Query = "archive",
            Status = DocumentStatus.Published,
            Page = 1,
            PageSize = 20,
            SortBy = "CreatedAt",
            Desc = true
        });

        response.EnsureSuccessStatusCode();
        return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private static async Task<List<double>> MeasureManyAsync(int iterations, Func<int, Task<double>> action)
    {
        var samples = new List<double>(iterations);

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            samples.Add(await action(iteration));
        }

        return samples;
    }

    private static PerformanceMetric BuildMetric(string name, string scenario, IReadOnlyList<double> samples)
    {
        var ordered = samples.OrderBy(value => value).ToArray();

        return new PerformanceMetric
        {
            Name = name,
            Scenario = scenario,
            Iterations = ordered.Length,
            AverageMs = ordered.Average(),
            MedianMs = Percentile(ordered, 0.50),
            P95Ms = Percentile(ordered, 0.95),
            MinMs = ordered.First(),
            MaxMs = ordered.Last()
        };
    }

    private static double Percentile(IReadOnlyList<double> ordered, double percentile)
    {
        if (ordered.Count == 1)
        {
            return ordered[0];
        }

        var position = (ordered.Count - 1) * percentile;
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
        {
            return ordered[lowerIndex];
        }

        var weight = position - lowerIndex;
        return ordered[lowerIndex] + (ordered[upperIndex] - ordered[lowerIndex]) * weight;
    }

    private static string BuildMarkdown(PerformanceReport report)
    {
        var lines = new List<string>
        {
            "# Wathiq Performance Baseline",
            string.Empty,
            $"- Generated at: `{report.GeneratedAtUtc:O}`",
            $"- Environment: `{report.Environment}`",
            $"- Search dataset size: `{report.SearchDatasetSize}` documents",
            $"- OCR iterations: `{report.OcrIterations}`",
            $"- Search iterations: `{report.SearchIterations}`",
            $"- Searchable-after-callback iterations: `{report.SearchableAfterCallbackIterations}`",
            string.Empty,
            "## Metrics",
            string.Empty,
            "| Metric | Scenario | Avg (ms) | Median (ms) | P95 (ms) | Min (ms) | Max (ms) |",
            "| --- | --- | ---: | ---: | ---: | ---: | ---: |"
        };

        lines.AddRange(report.Metrics.Select(metric =>
            $"| {metric.Name} | {metric.Scenario} | {metric.AverageMs:N2} | {metric.MedianMs:N2} | {metric.P95Ms:N2} | {metric.MinMs:N2} | {metric.MaxMs:N2} |"));

        lines.Add(string.Empty);
        lines.Add("## Interpretation");
        lines.Add(string.Empty);
        lines.Add("- This baseline measures API/controller/service latency inside the in-memory ASP.NET Core test host.");
        lines.Add("- It is ideal for regression tracking on the `Testing` branch, not for claiming production SLA values.");
        lines.Add("- `OCR callback persistence` reflects how quickly the API stores OCR text and triggers indexing.");
        lines.Add("- `Callback to searchable` reflects how quickly a processed document becomes discoverable through search.");
        lines.Add("- `Search latency` reflects role-aware query handling over a seeded searchable corpus.");

        return string.Join(Environment.NewLine, lines);
    }
}

internal sealed class PerformanceBaselineOptions
{
    public int OcrIterations { get; init; } = 25;
    public int SearchIterations { get; init; } = 50;
    public int SearchableAfterCallbackIterations { get; init; } = 20;
    public int SearchDatasetSize { get; init; } = 300;
}

internal sealed class PerformanceReport
{
    public DateTime GeneratedAtUtc { get; init; }
    public string Environment { get; init; } = string.Empty;
    public int SearchDatasetSize { get; init; }
    public int OcrIterations { get; init; }
    public int SearchIterations { get; init; }
    public int SearchableAfterCallbackIterations { get; init; }
    public List<PerformanceMetric> Metrics { get; init; } = [];
}

internal sealed class PerformanceMetric
{
    public string Name { get; init; } = string.Empty;
    public string Scenario { get; init; } = string.Empty;
    public int Iterations { get; init; }
    public double AverageMs { get; init; }
    public double MedianMs { get; init; }
    public double P95Ms { get; init; }
    public double MinMs { get; init; }
    public double MaxMs { get; init; }
}
