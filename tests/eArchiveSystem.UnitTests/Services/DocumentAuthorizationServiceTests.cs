using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Security;
using eArchiveSystem.Application.Services;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.UnitTests.Services;

[Trait("Layer", "Unit")]
[Trait("Area", "Authorization")]
public class DocumentAuthorizationServiceTests
{
    private readonly DocumentAuthorizationService _service = new(); 

    [Fact]
    public void CanView_AllowsSystemAdminToViewAnyDocument()
    {
        var actor = CreateUser("admin-1", ApplicationRoles.SystemAdmin, "inst-a", "dept-a");
        var document = CreateDocument("doc-1", "owner-1", DocumentStatus.Published, "inst-b", "dept-z");

        var result = _service.CanView(actor, document);

        Assert.True(result);
    }

    [Fact]
    public void CanView_DeniesManagerFromDifferentDepartment()
    {
        var actor = CreateUser("manager-1", ApplicationRoles.Manager, "inst-a", "dept-a");
        var document = CreateDocument("doc-1", "owner-1", DocumentStatus.Submitted, "inst-a", "dept-b");

        var result = _service.CanView(actor, document);

        Assert.False(result);
    }

    [Fact]
    public void CanEdit_AllowsEmployeeToEditOwnDraft()
    {
        var actor = CreateUser("employee-1", ApplicationRoles.Employee, "inst-a", "dept-a");
        var document = CreateDocument("doc-1", actor.Id, DocumentStatus.Draft, "inst-a", "dept-a");

        var result = _service.CanEdit(actor, document);

        Assert.True(result);
    }

    [Fact]
    public void CanDelete_AllowsInstitutionAdminToDeleteProcessingDocumentInsideInstitution()
    {
        var actor = CreateUser("inst-admin-1", ApplicationRoles.InstitutionAdmin, "inst-a", "dept-a");
        var document = CreateDocument("doc-1", "owner-1", DocumentStatus.Processing, "inst-a", "dept-b");

        var result = _service.CanDelete(actor, document);

        Assert.True(result);
    }

    [Fact]
    public void CanDelete_DeniesDeletingPublishedDocument()
    {
        var actor = CreateUser("employee-1", ApplicationRoles.Employee, "inst-a", "dept-a");
        var document = CreateDocument("doc-1", actor.Id, DocumentStatus.Published, "inst-a", "dept-a");

        var result = _service.CanDelete(actor, document);

        Assert.False(result);
    }

    [Fact]
    public void BuildSearchScope_ForManagerIncludesInstitutionAndDepartment()
    {
        var actor = CreateUser("manager-1", ApplicationRoles.Manager, "inst-a", "dept-a");

        SearchAccessScope scope = _service.BuildSearchScope(actor);

        Assert.Equal("inst-a", scope.InstitutionId);
        Assert.Equal("dept-a", scope.DepartmentId);
        Assert.Null(scope.OwnerUserId);
    }

    [Fact]
    public void BuildSearchScope_ForEmployeeRestrictsResultsToOwner()
    {
        var actor = CreateUser("employee-1", ApplicationRoles.Employee, "inst-a", "dept-a");

        SearchAccessScope scope = _service.BuildSearchScope(actor);

        Assert.Equal(actor.Id, scope.OwnerUserId);
        Assert.Null(scope.InstitutionId);
        Assert.Null(scope.DepartmentId);
    }

    [Fact]
    public void CanApprove_AllowsManagerToApproveUnderReviewDocumentInSameScope()
    {
        var actor = CreateUser("manager-1", ApplicationRoles.Manager, "inst-a", "dept-a");
        var document = CreateDocument("doc-1", "owner-1", DocumentStatus.UnderReview, "inst-a", "dept-a");

        var result = _service.CanApprove(actor, document);

        Assert.True(result);
    }

    [Fact]
    public void CanPublish_DeniesInstitutionAdminFromDifferentInstitution()
    {
        var actor = CreateUser("inst-admin-1", ApplicationRoles.InstitutionAdmin, "inst-a", "dept-a");
        var document = CreateDocument("doc-1", "owner-1", DocumentStatus.Approved, "inst-b", "dept-a");

        var result = _service.CanPublish(actor, document);

        Assert.False(result);
    }

    [Fact]
    public void CanTransfer_DeniesManagerWhenTargetDepartmentBelongsToAnotherInstitution()
    {
        var actor = CreateUser("manager-1", ApplicationRoles.Manager, "inst-a", "dept-a");
        var document = CreateDocument("doc-1", "owner-1", DocumentStatus.Submitted, "inst-a", "dept-a");
        var targetDepartment = new Department
        {
            Id = "dept-z",
            Name = "Other Institution Department",
            InstitutionId = "inst-b"
        };

        var result = _service.CanTransfer(actor, document, targetDepartment);

        Assert.False(result);
    }

    private static User CreateUser(string id, string role, string? institutionId, string? departmentId)
    {
        return new User
        {
            Id = id,
            Name = $"{role}-{id}",
            Email = $"{id}@example.com",
            Password = "hashed",
            Role = role,
            InstitutionId = institutionId,
            DepartmentId = departmentId,
            Department = departmentId
        };
    }

    private static Document CreateDocument(
        string id,
        string ownerId,
        DocumentStatus status,
        string? institutionId,
        string? departmentId)
    {
        return new Document
        {
            Id = id,
            Title = "Quarterly Report",
            FilePath = "uploads/report.pdf",
            FileName = "report.pdf",
            ContentType = "application/pdf",
            FileHash = "hash-1",
            Size = 1024,
            UserId = ownerId,
            InstitutionId = institutionId,
            DepartmentId = departmentId,
            Department = departmentId,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
