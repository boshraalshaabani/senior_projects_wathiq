using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Security;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.IntegrationTests.Infrastructure;

namespace eArchiveSystem.IntegrationTests.TestDoubles;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly IntegrationTestState _state;

    public InMemoryUserRepository(IntegrationTestState state)
    {
        _state = state;
    }

    public Task<User> GetByIdAsync(string id) =>
        Task.FromResult(_state.Users.TryGetValue(id, out var user) ? user : null!);

    public Task<User> GetByEmailAsync(string email) =>
        Task.FromResult(_state.Users.Values.FirstOrDefault(
            user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))!);

    public Task CreateAsync(User user)
    {
        _state.Users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(string id, User user)
    {
        _state.Users[id] = user;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        _state.Users.Remove(id);
        return Task.CompletedTask;
    }

    public Task<List<User>> GetAllAsync() =>
        Task.FromResult(_state.Users.Values.ToList());

    public Task<User> GetByResetToken(string token) =>
        Task.FromResult(_state.Users.Values.FirstOrDefault(user => user.ResetCode == token)!);

    public Task<List<User>> GetByRoleAsync(string role) =>
        Task.FromResult(_state.Users.Values.Where(user => user.Role == role).ToList());

    public Task<List<User>> GetByIdsAsync(List<string> ids) =>
        Task.FromResult(_state.Users.Values.Where(user => ids.Contains(user.Id)).ToList());
}

internal sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly IntegrationTestState _state;

    public InMemoryDocumentRepository(IntegrationTestState state)
    {
        _state = state;
    }

    public Task<Document?> GetByIdAsync(string id) =>
        Task.FromResult(_state.Documents.TryGetValue(id, out var document) ? document : null);

    public Task<Document> GetByHashAsync(string fileHash) =>
        Task.FromResult(_state.Documents.Values.FirstOrDefault(document => document.FileHash == fileHash)!);

    public Task CreateAsync(Document document)
    {
        _state.Documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task<List<Document>> GetByUserAsync(string userId) =>
        Task.FromResult(_state.Documents.Values.Where(document => document.UserId == userId).ToList());

    public Task UpdateAsync(string id, Document document)
    {
        document.UpdatedAt = DateTime.UtcNow;
        _state.Documents[id] = document;
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(string id, DocumentStatus status)
    {
        if (_state.Documents.TryGetValue(id, out var document))
        {
            document.Status = status;
            document.UpdatedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id) =>
        Task.FromResult(_state.Documents.Remove(id));

    public Task<List<Document>> GetAllAsync() =>
        Task.FromResult(_state.Documents.Values.ToList());

    public Task<List<Document>> GetByIdsAsync(IReadOnlyCollection<string> ids) =>
        Task.FromResult(_state.Documents.Values.Where(document => ids.Contains(document.Id)).ToList());

    public Task AttachMetadataAsync(string documentId)
    {
        if (_state.Documents.TryGetValue(documentId, out var document) &&
            _state.Metadata.TryGetValue(documentId, out var metadata))
        {
            document.Metadata = metadata;
        }

        return Task.CompletedTask;
    }

    public Task UpdateMetadataFieldsAsync(string documentId, Metadata metadata)
    {
        if (_state.Documents.TryGetValue(documentId, out var document))
        {
            document.Metadata = metadata;
            document.Department = metadata.Department;
            document.DepartmentId = metadata.DepartmentId;
            document.UpdatedAt = DateTime.UtcNow;
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
        if (_state.Documents.TryGetValue(documentId, out var document))
        {
            document.Content = content;
            document.RawOcrText = rawOcrText;
            document.NormalizedOcrText = normalizedOcrText;
            document.OcrProvider = ocrProvider;
            document.OcrLanguage = ocrLanguage;
            document.OcrPages = ocrPages;
            document.Department = department;
            document.DepartmentId = departmentId;
            document.OcrUpdatedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }
}

internal sealed class InMemoryMetadataRepository : IMetadataRepository
{
    private readonly IntegrationTestState _state;

    public InMemoryMetadataRepository(IntegrationTestState state)
    {
        _state = state;
    }

    public Task UpsertAsync(Metadata metadata)
    {
        _state.Metadata[metadata.Id] = metadata;
        return Task.CompletedTask;
    }

    public Task<Metadata?> GetByDocumentIdAsync(string documentId) =>
        Task.FromResult(_state.Metadata.TryGetValue(documentId, out var metadata) ? metadata : null);

    public Task<bool> DeleteByDocumentIdAsync(string documentId) =>
        Task.FromResult(_state.Metadata.Remove(documentId));
}

internal sealed class InMemoryDepartmentRepository : IDepartmentRepository
{
    private readonly IntegrationTestState _state;

    public InMemoryDepartmentRepository(IntegrationTestState state)
    {
        _state = state;
    }

    public Task CreateAsync(Department department)
    {
        _state.Departments[department.Id] = department;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(string id, Department department)
    {
        _state.Departments[id] = department;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id) =>
        Task.FromResult(_state.Departments.Remove(id));

    public Task<Department?> GetByIdAsync(string id) =>
        Task.FromResult(_state.Departments.TryGetValue(id, out var department) ? department : null);

    public Task<Department?> GetByNameAsync(string institutionId, string name) =>
        Task.FromResult(_state.Departments.Values.FirstOrDefault(
            department => department.InstitutionId == institutionId && department.Name == name));

    public Task<List<Department>> GetByInstitutionIdAsync(string institutionId) =>
        Task.FromResult(_state.Departments.Values.Where(
            department => department.InstitutionId == institutionId).ToList());
}

internal sealed class TrackingAuditService : IAuditService
{
    private readonly IntegrationTestState _state;

    public TrackingAuditService(IntegrationTestState state)
    {
        _state = state;
    }

    public Task LogAsync(string userId, string role, string action, string? documentId, string description)
    {
        _state.AuditEntries.Add((userId, action, documentId, description));
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
    private readonly IntegrationTestState _state;

    public TrackingIndexingService(IntegrationTestState state)
    {
        _state = state;
    }

    public Task SyncDocumentAsync(string documentId)
    {
        _state.IndexedDocumentIds.Add(documentId);
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
    private readonly IntegrationTestState _state;

    public TrackingNotificationService(IntegrationTestState state)
    {
        _state = state;
    }

    public Task<NotificationsPageDto> GetMyNotificationsAsync(string userId, bool unreadOnly, int page, int pageSize) =>
        Task.FromResult(new NotificationsPageDto());

    public Task<long> GetUnreadCountAsync(string userId) => Task.FromResult(0L);

    public Task MarkAsReadAsync(string userId, string notificationId) => Task.CompletedTask;

    public Task MarkAllAsReadAsync(string userId) => Task.CompletedTask;

    public Task NotifyDocumentUpdatedAsync(Document document, User actor) => Task.CompletedTask;

    public Task NotifyDocumentApprovedAsync(Document document, User actor)
    {
        _state.ApprovedDocumentIds.Add(document.Id);
        return Task.CompletedTask;
    }

    public Task NotifyDocumentRejectedAsync(Document document, User actor, string? reason)
    {
        _state.RejectedDocumentIds.Add(document.Id);
        return Task.CompletedTask;
    }

    public Task NotifyDocumentTransferredAsync(
        Document document,
        User actor,
        string? previousDepartmentName,
        Department targetDepartment,
        string justification)
    {
        _state.TransferredDocumentIds.Add(document.Id);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingEmailService : IEmailService
{
    private readonly IntegrationTestState _state;

    public RecordingEmailService(IntegrationTestState state)
    {
        _state = state;
    }

    public Task SendEmailAsync(string to, string subject, string body)
    {
        _state.SentEmails.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

internal sealed class TestPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"test-hash::{password}";

    public bool Verify(string password, string hashedPassword) =>
        string.Equals(Hash(password), hashedPassword, StringComparison.Ordinal);
}
