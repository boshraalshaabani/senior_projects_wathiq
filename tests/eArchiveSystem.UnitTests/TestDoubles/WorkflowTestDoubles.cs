using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.UnitTests.TestDoubles;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<string, User> _users;

    public InMemoryUserRepository(params User[] users)
    {
        _users = users.ToDictionary(user => user.Id);
    }

    public Task<User> GetByIdAsync(string id) =>
        Task.FromResult(_users.TryGetValue(id, out var user) ? user : null!);

    public Task<User> GetByEmailAsync(string email) =>
        Task.FromResult(_users.Values.FirstOrDefault(user => user.Email == email)!);

    public Task CreateAsync(User user)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(string id, User user)
    {
        _users[id] = user;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        _users.Remove(id);
        return Task.CompletedTask;
    }

    public Task<List<User>> GetAllAsync() =>
        Task.FromResult(_users.Values.ToList());

    public Task<User> GetByResetToken(string token) =>
        Task.FromResult(_users.Values.FirstOrDefault(user => user.ResetCode == token)!);

    public Task<List<User>> GetByRoleAsync(string role) =>
        Task.FromResult(_users.Values.Where(user => user.Role == role).ToList());

    public Task<List<User>> GetByIdsAsync(List<string> ids) =>
        Task.FromResult(_users.Values.Where(user => ids.Contains(user.Id)).ToList());
}

internal sealed class InMemoryDocumentRepository : IDocumentRepository
{
    public Dictionary<string, Document> Documents { get; }

    public InMemoryDocumentRepository(params Document[] documents)
    {
        Documents = documents.ToDictionary(document => document.Id);
    }

    public Task<Document?> GetByIdAsync(string id) =>
        Task.FromResult(Documents.TryGetValue(id, out var document) ? document : null);

    public Task UpdateAsync(string id, Document document)
    {
        Documents[id] = document;
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(string id, DocumentStatus status)
    {
        if (Documents.TryGetValue(id, out var document))
        {
            document.Status = status;
        }

        return Task.CompletedTask;
    }

    public Task UpdateMetadataFieldsAsync(string documentId, Metadata metadata)
    {
        if (Documents.TryGetValue(documentId, out var document))
        {
            document.Metadata = metadata;
            document.Department = metadata.Department;
            document.DepartmentId = metadata.DepartmentId;
        }

        return Task.CompletedTask;
    }

    public Task UpdateContentAsync(
        string documentId,
        string content,
        string rawOcrText,
        string normalizedOcrText,
        string? ocrProvider,
        string? ocrLanguage,
        int? ocrPages,
        string? department,
        string? departmentId)
    {
        if (Documents.TryGetValue(documentId, out var document))
        {
            document.Content = content;
            document.RawOcrText = rawOcrText;
            document.NormalizedOcrText = normalizedOcrText;
            document.OcrProvider = ocrProvider;
            document.OcrLanguage = ocrLanguage;
            document.OcrPages = ocrPages;
            document.Department = department;
            document.DepartmentId = departmentId;
        }

        return Task.CompletedTask;
    }

    public Task<Document> GetByHashAsync(string fileHash) =>
        Task.FromResult(Documents.Values.FirstOrDefault(document => document.FileHash == fileHash)!);

    public Task CreateAsync(Document document)
    {
        Documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task<List<Document>> GetByUserAsync(string userId) =>
        Task.FromResult(Documents.Values.Where(document => document.UserId == userId).ToList());

    public Task<bool> DeleteAsync(string id) =>
        Task.FromResult(Documents.Remove(id));

    public Task<List<Document>> GetAllAsync() =>
        Task.FromResult(Documents.Values.ToList());

    public Task<List<Document>> GetByIdsAsync(IReadOnlyCollection<string> ids) =>
        Task.FromResult(Documents.Values.Where(document => ids.Contains(document.Id)).ToList());

    public Task AttachMetadataAsync(string documentId) => Task.CompletedTask;
}

internal sealed class InMemoryMetadataRepository : IMetadataRepository
{
    private Metadata? _metadata;

    public InMemoryMetadataRepository(Metadata? metadata)
    {
        _metadata = metadata;
    }

    public Task<Metadata?> GetByDocumentIdAsync(string documentId) =>
        Task.FromResult(_metadata is not null && _metadata.Id == documentId ? _metadata : null);

    public Task UpsertAsync(Metadata metadata)
    {
        _metadata = metadata;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteByDocumentIdAsync(string documentId)
    {
        if (_metadata?.Id == documentId)
        {
            _metadata = null;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}

internal sealed class InMemoryDepartmentRepository : IDepartmentRepository
{
    private readonly Dictionary<string, Department> _departments;

    public InMemoryDepartmentRepository(params Department[] departments)
    {
        _departments = departments.ToDictionary(department => department.Id);
    }

    public Task<Department?> GetByIdAsync(string id) =>
        Task.FromResult(_departments.TryGetValue(id, out var department) ? department : null);

    public Task<Department?> GetByNameAsync(string institutionId, string name) =>
        Task.FromResult(_departments.Values.FirstOrDefault(
            department => department.InstitutionId == institutionId && department.Name == name));

    public Task<List<Department>> GetByInstitutionIdAsync(string institutionId) =>
        Task.FromResult(_departments.Values.Where(department => department.InstitutionId == institutionId).ToList());

    public Task CreateAsync(Department department)
    {
        _departments[department.Id] = department;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(string id, Department department)
    {
        _departments[id] = department;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id) => Task.FromResult(_departments.Remove(id));
}

internal sealed class TrackingAuditService : IAuditService
{
    public List<(string UserId, string Action, string? DocumentId, string Description)> Entries { get; } = new();

    public Task LogAsync(string userId, string role, string action, string? documentId, string description)
    {
        Entries.Add((userId, action, documentId, description));
        return Task.CompletedTask;
    }

    public Task<List<AuditLog>> GetAllAsync() => Task.FromResult(new List<AuditLog>());

    public Task<(List<AuditLog> Logs, long TotalCount)> GetFilteredAsync(
        string? userId,
        string? role,
        string? action,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize) =>
        Task.FromResult((new List<AuditLog>(), 0L));

    public Task<List<AuditLogDto>> GetAllWithUsersAsync(string requesterId) =>
        Task.FromResult(new List<AuditLogDto>());
}

internal sealed class TrackingIndexingService : IIndexingService
{
    public List<string> SyncedDocumentIds { get; } = new();

    public Task SyncDocumentAsync(string documentId)
    {
        SyncedDocumentIds.Add(documentId);
        return Task.CompletedTask;
    }

    public Task RemoveDocumentAsync(string documentId) => Task.CompletedTask;

    public Task EnsureIndexReadyAsync() => Task.CompletedTask;

    public Task ReindexAllAsync(bool recreateIndex = false) => Task.CompletedTask;

    public Task<(List<SearchDocumentIndex> Results, long Total)> SearchAsync(SearchDocumentsDto dto, SearchAccessScope scope) =>
        Task.FromResult((new List<SearchDocumentIndex>(), 0L));
}

internal sealed class TrackingNotificationService : INotificationService
{
    public List<string> ApprovedDocumentIds { get; } = new();
    public List<string> RejectedDocumentIds { get; } = new();
    public List<string> TransferredDocumentIds { get; } = new();
    public List<string> UpdatedDocumentIds { get; } = new();

    public Task<NotificationsPageDto> GetMyNotificationsAsync(string userId, bool unreadOnly, int page, int pageSize) =>
        Task.FromResult(new NotificationsPageDto());

    public Task<long> GetUnreadCountAsync(string userId) => Task.FromResult(0L);

    public Task MarkAsReadAsync(string userId, string notificationId) => Task.CompletedTask;

    public Task MarkAllAsReadAsync(string userId) => Task.CompletedTask;

    public Task NotifyDocumentUpdatedAsync(Document document, User actor)
    {
        UpdatedDocumentIds.Add(document.Id);
        return Task.CompletedTask;
    }

    public Task NotifyDocumentApprovedAsync(Document document, User actor)
    {
        ApprovedDocumentIds.Add(document.Id);
        return Task.CompletedTask;
    }

    public Task NotifyDocumentRejectedAsync(Document document, User actor, string? reason)
    {
        RejectedDocumentIds.Add(document.Id);
        return Task.CompletedTask;
    }

    public Task NotifyDocumentTransferredAsync(
        Document document,
        User actor,
        string? previousDepartmentName,
        Department targetDepartment,
        string justification)
    {
        TransferredDocumentIds.Add(document.Id);
        return Task.CompletedTask;
    }
}
