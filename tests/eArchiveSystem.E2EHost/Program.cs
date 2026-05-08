using System.Text;
using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Security;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Application.Security;
using eArchiveSystem.Application.Services;
using eArchiveSystem.Domain.Models;
using eArchiveSystem.Infrastructure.Security;
using eArchiveSystem.Presentation.Middleware;
using eArchiveSystem.TestHost.Infrastructure;
using eArchiveSystem.TestHost.TestDoubles;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

const string JwtKey = "Wathiq_Local_Development_Key_Change_Me_2026";
const string JwtIssuer = "eArchiveSystem";
const string JwtAudience = "eArchiveSystemUsers";

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers()
    .PartManager.ApplicationParts.Add(new AssemblyPart(typeof(eArchiveSystem.Controllers.AuthController).Assembly));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://127.0.0.1:4173", "http://localhost:4173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = JwtIssuer,
            ValidAudience = JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey))
        };
    });

var state = new IntegrationTestState();
SeedState(state);

builder.Services.AddSingleton(state);

builder.Services.RemoveAll<IUserRepository>();
builder.Services.RemoveAll<IDocumentRepository>();
builder.Services.RemoveAll<IMetadataRepository>();
builder.Services.RemoveAll<IDepartmentRepository>();
builder.Services.RemoveAll<IAuditRepository>();
builder.Services.RemoveAll<IAuditService>();
builder.Services.RemoveAll<IEmailService>();
builder.Services.RemoveAll<IIndexingService>();
builder.Services.RemoveAll<INotificationService>();
builder.Services.RemoveAll<IPasswordHasher>();
builder.Services.RemoveAll<ITokenService>();

builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
builder.Services.AddSingleton<IMetadataRepository, InMemoryMetadataRepository>();
builder.Services.AddSingleton<IDepartmentRepository, InMemoryDepartmentRepository>();
builder.Services.AddSingleton<IAuditRepository, InMemoryAuditRepository>();
builder.Services.AddSingleton<IAuditService, TrackingAuditService>();
builder.Services.AddSingleton<IEmailService, RecordingEmailService>();
builder.Services.AddSingleton<IIndexingService, TrackingIndexingService>();
builder.Services.AddSingleton<INotificationService, TrackingNotificationService>();
builder.Services.AddSingleton<IPasswordHasher, TestPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAnalyticsScopeService, AnalyticsScopeService>();
builder.Services.AddScoped<IDocumentAuthorizationService, DocumentAuthorizationService>();

builder.Services.AddScoped<IDocumentService, StubDocumentService>();
builder.Services.AddScoped<IDocumentTimelineService, StubDocumentTimelineService>();
builder.Services.AddScoped<IMetadataService, StubMetadataService>();
builder.Services.AddScoped<IMetadataPreviewService, StubMetadataPreviewService>();

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Jwt:Key"] = JwtKey,
    ["Jwt:Issuer"] = JwtIssuer,
    ["Jwt:Audience"] = JwtAudience,
    ["BootstrapAdmin:Name"] = string.Empty,
    ["BootstrapAdmin:Email"] = string.Empty,
    ["BootstrapAdmin:Password"] = string.Empty
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

await app.RunAsync("http://127.0.0.1:5281");

static void SeedState(IntegrationTestState state)
{
    var manager = new User
    {
        Id = "manager-search-1",
        Name = "Search Manager",
        Email = "search-manager@example.com",
        Password = new TestPasswordHasher().Hash("Pass123!"),
        Role = ApplicationRoles.Manager,
        InstitutionId = "inst-a",
        DepartmentId = "dept-a",
        Department = "Records"
    };

    var employee = new User
    {
        Id = "employee-search-1",
        Name = "Archive Employee",
        Email = "archive.employee@example.com",
        Password = new TestPasswordHasher().Hash("Pass123!"),
        Role = ApplicationRoles.Employee,
        InstitutionId = "inst-a",
        DepartmentId = "dept-a",
        Department = "Records"
    };

    state.Users[manager.Id] = manager;
    state.Users[employee.Id] = employee;

    var department = new Department
    {
        Id = "dept-a",
        Name = "Records",
        InstitutionId = "inst-a",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    state.Departments[department.Id] = department;

    var metadata = new Metadata
    {
        Id = "doc-search-allowed",
        Description = "Archive contract for operations testing.",
        Category = "Contract",
        Department = "Records",
        DepartmentId = "dept-a",
        DocumentType = "Contract",
        IssuingEntity = "Operations Directorate",
        ReferenceNumber = "ARCH-2026-001",
        Tags = new List<string> { "archive", "contract", "testing" }
    };

    var document = new Document
    {
        Id = "doc-search-allowed",
        Title = "Archive Contract 2026",
        Content = "Archive contract searchable content for the records department.",
        FilePath = "uploads/doc-search-allowed.pdf",
        FileName = "archive-contract-2026.pdf",
        ContentType = "application/pdf",
        FileHash = "hash-doc-search-allowed",
        Size = 1024,
        UserId = employee.Id,
        InstitutionId = "inst-a",
        DepartmentId = "dept-a",
        Department = "Records",
        Status = DocumentStatus.Published,
        Priority = DocumentPriority.Important,
        CreatedAt = DateTime.UtcNow.AddHours(-4),
        UpdatedAt = DateTime.UtcNow.AddHours(-1),
        Metadata = metadata
    };

    state.Metadata[metadata.Id] = metadata;
    state.Documents[document.Id] = document;
    state.IndexedDocumentIds.Add(document.Id);
    state.AuditLogs.Add(new AuditLog
    {
        Id = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow,
        UserId = employee.Id,
        UserRole = employee.Role,
        Action = "AddDocument",
        DocumentId = document.Id,
        Description = "Seeded document added for E2E search flow"
    });
}

file sealed class StubDocumentService : IDocumentService
{
    public Task<DocumentAddResult> AddDocumentAsync(string actorUserId, AddDocumentDto dto) => throw new NotSupportedException();
    public Task<Document> GetByIdAsync(string id) => throw new NotSupportedException();
    public Task DeleteDocumentAsync(string id, string userId, string role) => throw new NotSupportedException();
    public Task<DocumentViewDto> ViewDocumentAsync(string documentId, string userId, string role) => throw new NotSupportedException();
    public Task<DocumentOcrTextDto> GetExtractedTextAsync(string documentId, string userId, string role) => throw new NotSupportedException();
    public Task<(Stream FileStream, string FileName, string ContentType)> DownloadDocumentAsync(string documentId, string userId, string role) => throw new NotSupportedException();
    public Task<DocumentUpdateResult> UpdateDocumentAsync(string documentId, UpdateDocumentDto dto, string userId, string role) => throw new NotSupportedException();
    public Task AttachMetadataAsync(string documentId) => throw new NotSupportedException();
}

file sealed class StubDocumentTimelineService : IDocumentTimelineService
{
    public Task<DocumentTimelineDto> GetTimelineAsync(string documentId, string requesterId) => throw new NotSupportedException();
}

file sealed class StubMetadataService : IMetadataService
{
    public Task<bool> AddMetadataAsync(string documentId, AddMetadataDto dto, string userId, string role) => throw new NotSupportedException();
    public Task<Metadata?> ViewMetadataAsync(string documentId, string userId, string role) => throw new NotSupportedException();
    public Task<bool> UpdateMetadataAsync(string documentId, AddMetadataDto dto, string userId, string role) => throw new NotSupportedException();
}

file sealed class StubMetadataPreviewService : IMetadataPreviewService
{
    public Task<MetadataPreviewDto> GeneratePreviewAsync(string documentId, string userId, string role) => throw new NotSupportedException();
}


