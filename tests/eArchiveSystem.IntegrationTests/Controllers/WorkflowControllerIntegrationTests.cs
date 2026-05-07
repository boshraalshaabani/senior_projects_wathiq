using eArchiveSystem.TestHost.Infrastructure;

namespace eArchiveSystem.IntegrationTests.Controllers;

[Trait("Layer", "Integration")]
[Trait("Area", "Workflow")]
public class WorkflowControllerIntegrationTests
{
    [Fact]
    public async Task StartReview_ReturnsUnauthorized_WhenRequestHasNoToken()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsync("/api/documents/doc-1/workflow/start-review", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartReview_TransitionsSubmittedDocument_ForAuthorizedManager()
    {
        using var factory = new TestWebApplicationFactory();

        var manager = new User
        {
            Id = "manager-1",
            Name = "Integration Manager",
            Email = "manager@example.com",
            Password = factory.HashPassword("Pass123!"),
            Role = ApplicationRoles.Manager,
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "Records"
        };

        var document = new Document
        {
            Id = "doc-workflow-1",
            Title = "Workflow Candidate",
            FilePath = "uploads/workflow.pdf",
            FileName = "workflow.pdf",
            ContentType = "application/pdf",
            FileHash = "hash-workflow-1",
            Size = 1024,
            UserId = "employee-1",
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "Records",
            Status = DocumentStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        factory.State.Users[manager.Id] = manager;
        factory.State.Documents[document.Id] = document;

        using var client = factory.CreateAuthenticatedClient(manager);

        var response = await client.PostAsync($"/api/documents/{document.Id}/workflow/start-review", content: null);

        response.EnsureSuccessStatusCode();

        Assert.Equal(DocumentStatus.UnderReview, factory.State.Documents[document.Id].Status);
        Assert.Contains(document.Id, factory.State.IndexedDocumentIds);
        Assert.Contains(factory.State.AuditEntries, entry => entry.Action == "StartReview");
    }
}
