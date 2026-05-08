using System.Text.Json;
using eArchiveSystem.TestHost.Infrastructure;

namespace eArchiveSystem.IntegrationTests.Controllers;

[Trait("Layer", "Integration")]
[Trait("Area", "Dashboard")]
public class DashboardControllerIntegrationTests
{
    [Fact]
    public async Task GetTotals_ReturnsScopedCounts_ForAuthorizedManager()
    {
        using var factory = new TestWebApplicationFactory();

        var manager = new User
        {
            Id = "manager-dashboard-1",
            Name = "Dashboard Manager",
            Email = "dashboard.manager@example.com",
            Password = factory.HashPassword("Pass123!"),
            Role = ApplicationRoles.Manager,
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "Records"
        };

        var sameDepartmentEmployee = new User
        {
            Id = "employee-dashboard-1",
            Name = "Same Department Employee",
            Email = "records.employee@example.com",
            Password = factory.HashPassword("Pass123!"),
            Role = ApplicationRoles.Employee,
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "Records"
        };

        var otherDepartmentEmployee = new User
        {
            Id = "employee-dashboard-2",
            Name = "Other Department Employee",
            Email = "finance.employee@example.com",
            Password = factory.HashPassword("Pass123!"),
            Role = ApplicationRoles.Employee,
            InstitutionId = "inst-a",
            DepartmentId = "dept-b",
            Department = "Finance"
        };

        factory.State.Users[manager.Id] = manager;
        factory.State.Users[sameDepartmentEmployee.Id] = sameDepartmentEmployee;
        factory.State.Users[otherDepartmentEmployee.Id] = otherDepartmentEmployee;

        factory.State.Documents["dashboard-doc-1"] = new Document
        {
            Id = "dashboard-doc-1",
            Title = "Records Document",
            FilePath = "uploads/dashboard-doc-1.pdf",
            FileName = "dashboard-doc-1.pdf",
            ContentType = "application/pdf",
            FileHash = "hash-dashboard-doc-1",
            Size = 1024,
            UserId = sameDepartmentEmployee.Id,
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "Records",
            Status = DocumentStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        factory.State.Documents["dashboard-doc-2"] = new Document
        {
            Id = "dashboard-doc-2",
            Title = "Finance Document",
            FilePath = "uploads/dashboard-doc-2.pdf",
            FileName = "dashboard-doc-2.pdf",
            ContentType = "application/pdf",
            FileHash = "hash-dashboard-doc-2",
            Size = 1024,
            UserId = otherDepartmentEmployee.Id,
            InstitutionId = "inst-a",
            DepartmentId = "dept-b",
            Department = "Finance",
            Status = DocumentStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        factory.State.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            UserId = sameDepartmentEmployee.Id,
            UserRole = sameDepartmentEmployee.Role,
            Action = "AddDocument",
            DocumentId = "dashboard-doc-1",
            Description = "Visible upload"
        });

        factory.State.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            UserId = otherDepartmentEmployee.Id,
            UserRole = otherDepartmentEmployee.Role,
            Action = "UpdateDocument",
            DocumentId = "dashboard-doc-2",
            Description = "Hidden update"
        });

        using var client = factory.CreateAuthenticatedClient(manager);

        var response = await client.GetAsync("/api/dashboard/totals");

        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        Assert.Equal(1, root.GetProperty("totalDocuments").GetInt32());
        Assert.Equal(2, root.GetProperty("totalUsers").GetInt32());
        Assert.Equal(1, root.GetProperty("todayUploads").GetInt32());
        Assert.Equal(0, root.GetProperty("monthlyUpdates").GetInt32());
    }
}
