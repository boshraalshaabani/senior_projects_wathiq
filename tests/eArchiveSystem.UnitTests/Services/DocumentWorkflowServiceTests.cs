using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Security;
using eArchiveSystem.Application.Services;
using eArchiveSystem.Domain.Models;
using eArchiveSystem.UnitTests.TestDoubles;

namespace eArchiveSystem.UnitTests.Services;

[Trait("Layer", "Unit")]
[Trait("Area", "Workflow")]
public class DocumentWorkflowServiceTests
{
    [Fact]
    public async Task SubmitDocumentAsync_FailsWhenMetadataIsMissing()
    {
        const string actorId = "employee-1";
        const string documentId = "doc-1";

        var actor = CreateUser(actorId, ApplicationRoles.Employee, "inst-a", "dept-a");
        var document = CreateDocument(documentId, actorId, DocumentStatus.Draft, content: "ocr text");

        var service = CreateService(actor, document, metadata: null);

        var result = await service.SubmitDocumentAsync(actorId, documentId);

        Assert.False(result.Success);
        Assert.Equal("Document metadata is required before submission", result.Message);
    }

    [Fact]
    public async Task SubmitDocumentAsync_FailsWhenOcrOutputIsMissing()
    {
        const string actorId = "employee-1";
        const string documentId = "doc-1";

        var actor = CreateUser(actorId, ApplicationRoles.Employee, "inst-a", "dept-a");
        var document = CreateDocument(documentId, actorId, DocumentStatus.Draft, content: null);
        var metadata = CreateMetadata(documentId, "dept-a");

        var service = CreateService(actor, document, metadata);

        var result = await service.SubmitDocumentAsync(actorId, documentId);

        Assert.False(result.Success);
        Assert.Equal("OCR processing must be completed before submission", result.Message);
    }

    [Fact]
    public async Task SubmitDocumentAsync_UpdatesStatusAndSyncsIndexOnSuccess()
    {
        const string actorId = "employee-1";
        const string documentId = "doc-1";

        var actor = CreateUser(actorId, ApplicationRoles.Employee, "inst-a", "dept-a");
        var document = CreateDocument(documentId, actorId, DocumentStatus.Draft, content: "normalized text");
        var metadata = CreateMetadata(documentId, "dept-a");

        var users = new InMemoryUserRepository(actor);
        var documents = new InMemoryDocumentRepository(document);
        var metadataRepository = new InMemoryMetadataRepository(metadata);
        var authorization = new DocumentAuthorizationService();
        var audit = new TrackingAuditService();
        var indexing = new TrackingIndexingService();
        var notifications = new TrackingNotificationService();

        var service = new DocumentWorkflowService(
            users,
            documents,
            new InMemoryDepartmentRepository(),
            metadataRepository,
            authorization,
            audit,
            indexing,
            notifications);

        var result = await service.SubmitDocumentAsync(actorId, documentId);

        Assert.True(result.Success);
        Assert.Equal(DocumentStatus.Submitted, result.Data.Status);
        Assert.Equal(DocumentStatus.Submitted, documents.Documents[documentId].Status);
        Assert.Contains(documentId, indexing.SyncedDocumentIds);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task StartReviewAsync_TransitionsSubmittedDocumentAndSyncsIndex()
    {
        const string actorId = "manager-1";
        const string documentId = "doc-1";

        var actor = CreateUser(actorId, ApplicationRoles.Manager, "inst-a", "dept-a");
        var document = CreateDocument(documentId, "employee-1", DocumentStatus.Submitted, content: "ocr text");
        var users = new InMemoryUserRepository(actor);
        var documents = new InMemoryDocumentRepository(document);
        var audit = new TrackingAuditService();
        var indexing = new TrackingIndexingService();

        var service = new DocumentWorkflowService(
            users,
            documents,
            new InMemoryDepartmentRepository(),
            new InMemoryMetadataRepository(null),
            new DocumentAuthorizationService(),
            audit,
            indexing,
            new TrackingNotificationService());

        var result = await service.StartReviewAsync(actorId, documentId);

        Assert.True(result.Success);
        Assert.Equal(DocumentStatus.UnderReview, result.Data.Status);
        Assert.Equal(DocumentStatus.UnderReview, documents.Documents[documentId].Status);
        Assert.NotNull(result.Data.ReviewStartedAt);
        Assert.Contains(documentId, indexing.SyncedDocumentIds);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task ApproveDocumentAsync_ApprovesUnderReviewDocumentAndSendsNotification()
    {
        const string actorId = "manager-1";
        const string documentId = "doc-1";

        var actor = CreateUser(actorId, ApplicationRoles.Manager, "inst-a", "dept-a");
        var document = CreateDocument(documentId, "employee-1", DocumentStatus.UnderReview, content: "ocr text");
        var users = new InMemoryUserRepository(actor);
        var documents = new InMemoryDocumentRepository(document);
        var audit = new TrackingAuditService();
        var indexing = new TrackingIndexingService();
        var notifications = new TrackingNotificationService();

        var service = new DocumentWorkflowService(
            users,
            documents,
            new InMemoryDepartmentRepository(),
            new InMemoryMetadataRepository(null),
            new DocumentAuthorizationService(),
            audit,
            indexing,
            notifications);

        var result = await service.ApproveDocumentAsync(actorId, documentId);

        Assert.True(result.Success);
        Assert.Equal(DocumentStatus.Approved, result.Data.Status);
        Assert.Equal(actorId, documents.Documents[documentId].ReviewedByUserId);
        Assert.Contains(documentId, indexing.SyncedDocumentIds);
        Assert.Contains(documentId, notifications.ApprovedDocumentIds);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task RejectDocumentAsync_RequiresAComment()
    {
        var service = CreateService(
            CreateUser("employee-1", ApplicationRoles.Employee, "inst-a", "dept-a"),
            CreateDocument("doc-1", "employee-1", DocumentStatus.UnderReview, content: "ocr text"),
            CreateMetadata("doc-1", "dept-a"));

        var result = await service.RejectDocumentAsync("employee-1", "doc-1", new ReviewDecisionDto
        {
            Comment = string.Empty
        });

        Assert.False(result.Success);
        Assert.Equal("Rejection comment is required", result.Message);
    }

    [Fact]
    public async Task RejectDocumentAsync_RejectsUnderReviewDocumentAndStoresReason()
    {
        const string actorId = "manager-1";
        const string documentId = "doc-1";

        var actor = CreateUser(actorId, ApplicationRoles.Manager, "inst-a", "dept-a");
        var document = CreateDocument(documentId, "employee-1", DocumentStatus.UnderReview, content: "ocr text");
        var users = new InMemoryUserRepository(actor);
        var documents = new InMemoryDocumentRepository(document);
        var audit = new TrackingAuditService();
        var indexing = new TrackingIndexingService();
        var notifications = new TrackingNotificationService();

        var service = new DocumentWorkflowService(
            users,
            documents,
            new InMemoryDepartmentRepository(),
            new InMemoryMetadataRepository(null),
            new DocumentAuthorizationService(),
            audit,
            indexing,
            notifications);

        var result = await service.RejectDocumentAsync(actorId, documentId, new ReviewDecisionDto
        {
            Comment = "Missing signature"
        });

        Assert.True(result.Success);
        Assert.Equal(DocumentStatus.Rejected, result.Data.Status);
        Assert.Equal("Missing signature", documents.Documents[documentId].RejectionReason);
        Assert.Contains(documentId, indexing.SyncedDocumentIds);
        Assert.Contains(documentId, notifications.RejectedDocumentIds);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task PublishDocumentAsync_PublishesApprovedDocument()
    {
        const string actorId = "inst-admin-1";
        const string documentId = "doc-1";

        var actor = CreateUser(actorId, ApplicationRoles.InstitutionAdmin, "inst-a", "dept-a");
        var document = CreateDocument(documentId, "employee-1", DocumentStatus.Approved, content: "ocr text");
        var users = new InMemoryUserRepository(actor);
        var documents = new InMemoryDocumentRepository(document);
        var audit = new TrackingAuditService();
        var indexing = new TrackingIndexingService();

        var service = new DocumentWorkflowService(
            users,
            documents,
            new InMemoryDepartmentRepository(),
            new InMemoryMetadataRepository(null),
            new DocumentAuthorizationService(),
            audit,
            indexing,
            new TrackingNotificationService());

        var result = await service.PublishDocumentAsync(actorId, documentId);

        Assert.True(result.Success);
        Assert.Equal(DocumentStatus.Published, result.Data.Status);
        Assert.Equal(actorId, documents.Documents[documentId].PublishedByUserId);
        Assert.Contains(documentId, indexing.SyncedDocumentIds);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task TransferDocumentAsync_UpdatesDocumentAndMetadataDepartment()
    {
        const string actorId = "manager-1";
        const string documentId = "doc-1";

        var actor = CreateUser(actorId, ApplicationRoles.Manager, "inst-a", "dept-a");
        var document = CreateDocument(documentId, "employee-1", DocumentStatus.Submitted, content: "ocr text");
        var metadata = CreateMetadata(documentId, "dept-a");
        var targetDepartment = new Department
        {
            Id = "dept-b",
            Name = "Records Department",
            InstitutionId = "inst-a"
        };

        var users = new InMemoryUserRepository(actor);
        var documents = new InMemoryDocumentRepository(document);
        var metadataRepository = new InMemoryMetadataRepository(metadata);
        var audit = new TrackingAuditService();
        var indexing = new TrackingIndexingService();
        var notifications = new TrackingNotificationService();

        var service = new DocumentWorkflowService(
            users,
            documents,
            new InMemoryDepartmentRepository(targetDepartment),
            metadataRepository,
            new DocumentAuthorizationService(),
            audit,
            indexing,
            notifications);

        var result = await service.TransferDocumentAsync(actorId, documentId, new TransferDocumentDto
        {
            TargetDepartmentId = "dept-b",
            Justification = "  Workload balancing  "
        });

        var updatedMetadata = await metadataRepository.GetByDocumentIdAsync(documentId);

        Assert.True(result.Success);
        Assert.Equal("dept-b", documents.Documents[documentId].DepartmentId);
        Assert.Equal("Records Department", documents.Documents[documentId].Department);
        Assert.Equal("dept-b", updatedMetadata?.DepartmentId);
        Assert.Equal("Records Department", updatedMetadata?.Department);
        Assert.Equal("Workload balancing", result.Data.Justification);
        Assert.Contains(documentId, indexing.SyncedDocumentIds);
        Assert.Contains(documentId, notifications.TransferredDocumentIds);
        Assert.Single(audit.Entries);
    }

    private static DocumentWorkflowService CreateService(User actor, Document document, Metadata? metadata)
    {
        return new DocumentWorkflowService(
            new InMemoryUserRepository(actor),
            new InMemoryDocumentRepository(document),
            new InMemoryDepartmentRepository(),
            new InMemoryMetadataRepository(metadata),
            new DocumentAuthorizationService(),
            new TrackingAuditService(),
            new TrackingIndexingService(),
            new TrackingNotificationService());
    }

    private static User CreateUser(string id, string role, string? institutionId, string? departmentId)
    {
        return new User
        {
            Id = id,
            Name = id,
            Email = $"{id}@example.com",
            Password = "hashed",
            Role = role,
            InstitutionId = institutionId,
            DepartmentId = departmentId,
            Department = departmentId
        };
    }

    private static Document CreateDocument(string id, string ownerId, DocumentStatus status, string? content)
    {
        return new Document
        {
            Id = id,
            Title = "Document",
            FilePath = "uploads/doc.pdf",
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            FileHash = "hash-1",
            Size = 4096,
            UserId = ownerId,
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "dept-a",
            Status = status,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Metadata CreateMetadata(string id, string? departmentId)
    {
        return new Metadata
        {
            Id = id,
            Category = "Administrative",
            DepartmentId = departmentId,
            Department = departmentId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
