using eArchiveSystem.TestHost.Infrastructure;

namespace eArchiveSystem.IntegrationTests.Controllers;

[Trait("Layer", "Integration")]
[Trait("Area", "OCRCallback")]
public class OcrCallbackControllerIntegrationTests
{
    [Fact]
    public async Task Callback_StoresNormalizedContent_UpdatesStatus_AndIndexesDocument()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        var document = new Document
        {
            Id = "doc-ocr-1",
            Title = "OCR Candidate",
            FilePath = "uploads/doc-ocr-1.pdf",
            FileName = "doc-ocr-1.pdf",
            ContentType = "application/pdf",
            FileHash = "hash-ocr-1",
            Size = 2048,
            UserId = "employee-1",
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "Records",
            Status = DocumentStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        factory.State.Documents[document.Id] = document;

        var response = await client.PostAsJsonAsync(
            $"/api/ocr/callback?documentId={document.Id}",
            new OcrCallbackDto
            {
                Text = "the archive record 123",
                RawText = "THE archive record 123",
                NormalizedText = "the archive record 123",
                Language = "eng",
                Pages = 1,
                Provider = "integration-test"
            });

        response.EnsureSuccessStatusCode();

        var stored = factory.State.Documents[document.Id];

        Assert.Equal(DocumentStatus.Draft, stored.Status);
        Assert.Equal("archive record 123", stored.Content);
        Assert.Equal("THE archive record 123", stored.RawOcrText);
        Assert.Equal("the archive record 123", stored.NormalizedOcrText);
        Assert.Contains(document.Id, factory.State.IndexedDocumentIds);
    }
}
