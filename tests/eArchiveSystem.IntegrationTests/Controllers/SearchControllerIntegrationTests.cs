using eArchiveSystem.TestHost.Infrastructure;

namespace eArchiveSystem.IntegrationTests.Controllers;

[Trait("Layer", "Integration")]
[Trait("Area", "Search")]
public class SearchControllerIntegrationTests
{
    [Fact]
    public async Task Search_ReturnsOnlyDocumentsWithinManagerDepartmentScope()
    {
        using var factory = new TestWebApplicationFactory();

        var manager = new User
        {
            Id = "manager-search-1",
            Name = "Search Manager",
            Email = "search-manager@example.com",
            Password = factory.HashPassword("Pass123!"),
            Role = ApplicationRoles.Manager,
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "Records"
        };

        factory.State.Users[manager.Id] = manager;

        var allowedDocument = CreateDocument(
            id: "doc-search-allowed",
            title: "Archive Contract",
            userId: "employee-a",
            institutionId: "inst-a",
            departmentId: "dept-a",
            department: "Records");

        var blockedDocument = CreateDocument(
            id: "doc-search-blocked",
            title: "Archive Contract",
            userId: "employee-b",
            institutionId: "inst-a",
            departmentId: "dept-b",
            department: "Finance");

        factory.State.Documents[allowedDocument.Id] = allowedDocument;
        factory.State.Documents[blockedDocument.Id] = blockedDocument;
        factory.State.IndexedDocumentIds.Add(allowedDocument.Id);
        factory.State.IndexedDocumentIds.Add(blockedDocument.Id);

        using var client = factory.CreateAuthenticatedClient(manager);

        var response = await client.PostAsJsonAsync("/api/documents/search", new SearchDocumentsDto
        {
            Query = "Archive",
            Page = 1,
            PageSize = 10
        });

        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        var data = root.GetProperty("data");
        var items = data.EnumerateArray().ToList();

        Assert.Equal(1, root.GetProperty("total").GetInt64());
        Assert.Single(items);
        Assert.Equal(allowedDocument.Id, items[0].GetProperty("id").GetString());
    }

    private static Document CreateDocument(
        string id,
        string title,
        string userId,
        string institutionId,
        string departmentId,
        string department)
    {
        return new Document
        {
            Id = id,
            Title = title,
            Content = $"{title} searchable content",
            FilePath = $"uploads/{id}.pdf",
            FileName = $"{id}.pdf",
            ContentType = "application/pdf",
            FileHash = $"hash-{id}",
            Size = 1024,
            UserId = userId,
            InstitutionId = institutionId,
            DepartmentId = departmentId,
            Department = department,
            Status = DocumentStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
