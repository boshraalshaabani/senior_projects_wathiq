using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Security;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Domain.Models;
using eArchiveSystem.TestHost.Infrastructure;

namespace eArchiveSystem.TestHost.TestDoubles;

public sealed class InMemoryUserRepository : IUserRepository
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

public sealed class InMemoryDocumentRepository : IDocumentRepository
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

public sealed class InMemoryMetadataRepository : IMetadataRepository
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

public sealed class InMemoryDepartmentRepository : IDepartmentRepository
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


public sealed class InMemoryAuditRepository : IAuditRepository
{
    private readonly IntegrationTestState _state;

    public InMemoryAuditRepository(IntegrationTestState state)
    {
        _state = state;
    }

    public Task CreateAsync(AuditLog log)
    {
        if (string.IsNullOrWhiteSpace(log.Id))
        {
            log.Id = Guid.NewGuid().ToString();
        }

        if (log.Timestamp == default)
        {
            log.Timestamp = DateTime.UtcNow;
        }

        _state.AuditLogs.Add(log);
        _state.AuditEntries.Add((log.UserId, log.Action, string.IsNullOrWhiteSpace(log.DocumentId) ? null : log.DocumentId, log.Description));
        return Task.CompletedTask;
    }

    public Task<List<AuditLog>> GetByDocumentIdAsync(string documentId) =>
        Task.FromResult(_state.AuditLogs.Where(log => string.Equals(log.DocumentId, documentId, StringComparison.OrdinalIgnoreCase)).ToList());

    public Task<(List<AuditLog> Logs, long TotalCount)> GetFilteredAsync(
        string? userId,
        string? role,
        string? action,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize)
    {
        IEnumerable<AuditLog> query = _state.AuditLogs;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(log => string.Equals(log.UserId, userId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(log => string.Equals(log.UserRole, role, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(log => string.Equals(log.Action, action, StringComparison.OrdinalIgnoreCase));
        }

        if (from.HasValue)
        {
            query = query.Where(log => log.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(log => log.Timestamp <= to.Value);
        }

        var filtered = query.OrderByDescending(log => log.Timestamp).ToList();
        var currentPage = page <= 0 ? 1 : page;
        var currentPageSize = pageSize <= 0 ? 20 : pageSize;
        var paged = filtered.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize).ToList();
        return Task.FromResult((paged, (long)filtered.Count));
    }

    public Task<List<AuditLog>> GetAllAsync() => Task.FromResult(_state.AuditLogs.OrderByDescending(log => log.Timestamp).ToList());
}
public sealed class TrackingAuditService : IAuditService
{
    private readonly IntegrationTestState _state;

    public TrackingAuditService(IntegrationTestState state)
    {
        _state = state;
    }

    public Task LogAsync(string userId, string role, string action, string? documentId, string description)
    {
        _state.AuditEntries.Add((userId, action, documentId, description));
        _state.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            UserRole = role,
            Action = action,
            DocumentId = documentId ?? string.Empty,
            Description = description
        });
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

public sealed class TrackingIndexingService : IIndexingService
{
    private readonly IntegrationTestState _state;

    public TrackingIndexingService(IntegrationTestState state)
    {
        _state = state;
    }

    public Task SyncDocumentAsync(string documentId)
    {
        if (!_state.IndexedDocumentIds.Contains(documentId, StringComparer.OrdinalIgnoreCase))
        {
            _state.IndexedDocumentIds.Add(documentId);
        }

        return Task.CompletedTask;
    }

    public Task RemoveDocumentAsync(string documentId)
    {
        _state.IndexedDocumentIds.RemoveAll(id => string.Equals(id, documentId, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task EnsureIndexReadyAsync() => Task.CompletedTask;

    public Task ReindexAllAsync(bool recreateIndex = false)
    {
        _state.IndexedDocumentIds.Clear();
        _state.IndexedDocumentIds.AddRange(_state.Documents.Keys);
        return Task.CompletedTask;
    }

    public Task<(List<SearchDocumentIndex> Results, long Total)> SearchAsync(SearchDocumentsDto dto, SearchAccessScope scope)
    {
        IEnumerable<Document> documents = _state.Documents.Values
            .Where(document => _state.IndexedDocumentIds.Contains(document.Id, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(scope.OwnerUserId))
        {
            documents = documents.Where(document => string.Equals(document.UserId, scope.OwnerUserId, StringComparison.OrdinalIgnoreCase));
        }
        else if (!string.IsNullOrWhiteSpace(scope.InstitutionId))
        {
            documents = documents.Where(document => string.Equals(document.InstitutionId, scope.InstitutionId, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(scope.DepartmentId))
            {
                documents = documents.Where(document =>
                    string.Equals(document.DepartmentId ?? document.Department, scope.DepartmentId, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.Query))
        {
            documents = documents.Where(document => MatchesQuery(document, dto.Query));
        }

        if (!string.IsNullOrWhiteSpace(dto.Category))
        {
            documents = documents.Where(document =>
                string.Equals(document.Metadata?.Category, dto.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(dto.DepartmentId) || !string.IsNullOrWhiteSpace(dto.Department))
        {
            var requestedDepartment = dto.DepartmentId ?? dto.Department;
            documents = documents.Where(document =>
                string.Equals(document.DepartmentId ?? document.Department, requestedDepartment, StringComparison.OrdinalIgnoreCase));
        }

        if (dto.Status.HasValue)
        {
            documents = documents.Where(document => document.Status == dto.Status.Value);
        }

        if (dto.Priority.HasValue)
        {
            documents = documents.Where(document => document.Priority == dto.Priority.Value);
        }

        if (dto.FromDate.HasValue)
        {
            documents = documents.Where(document => document.CreatedAt >= dto.FromDate.Value);
        }

        if (dto.ToDate.HasValue)
        {
            documents = documents.Where(document => document.CreatedAt <= dto.ToDate.Value);
        }

        documents = ApplySorting(documents, dto);

        var total = documents.LongCount();
        var page = dto.Page <= 0 ? 1 : dto.Page;
        var pageSize = dto.PageSize <= 0 ? 10 : dto.PageSize;

        var paged = documents
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToIndex)
            .ToList();

        return Task.FromResult((paged, total));
    }

    private static IEnumerable<Document> ApplySorting(IEnumerable<Document> documents, SearchDocumentsDto dto)
    {
        var sortBy = dto.SortBy?.Trim().ToLowerInvariant();
        var descending = dto.Desc;

        return sortBy switch
        {
            "title" => descending
                ? documents.OrderByDescending(document => document.Title)
                : documents.OrderBy(document => document.Title),
            "updatedat" => descending
                ? documents.OrderByDescending(document => document.UpdatedAt)
                : documents.OrderBy(document => document.UpdatedAt),
            _ => descending
                ? documents.OrderByDescending(document => document.CreatedAt)
                : documents.OrderBy(document => document.CreatedAt)
        };
    }

    private static bool MatchesQuery(Document document, string query)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return (document.Title?.Contains(query, comparison) ?? false)
            || (document.Content?.Contains(query, comparison) ?? false)
            || (document.NormalizedOcrText?.Contains(query, comparison) ?? false)
            || (document.RawOcrText?.Contains(query, comparison) ?? false)
            || (document.Metadata?.Description?.Contains(query, comparison) ?? false)
            || (document.Metadata?.ReferenceNumber?.Contains(query, comparison) ?? false)
            || (document.Metadata?.Category?.Contains(query, comparison) ?? false);
    }

    private static SearchDocumentIndex MapToIndex(Document document)
    {
        return new SearchDocumentIndex
        {
            Id = document.Id,
            Title = document.Title,
            Content = document.Content ?? string.Empty,
            Description = document.Metadata?.Description,
            Snippet = CreateSnippet(document),
            InstitutionId = document.InstitutionId ?? string.Empty,
            DepartmentId = document.DepartmentId ?? document.Department ?? string.Empty,
            Department = document.Department ?? document.DepartmentId ?? string.Empty,
            UserId = document.UserId,
            IsSensitive = document.IsSensitive,
            Category = document.Metadata?.Category,
            DocumentType = document.Metadata?.DocumentType,
            IssuingEntity = document.Metadata?.IssuingEntity,
            ReferenceNumber = document.Metadata?.ReferenceNumber,
            Status = document.Status,
            Priority = document.Priority,
            Tags = document.Metadata?.Tags ?? new List<string>(),
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };
    }

    private static string? CreateSnippet(Document document)
    {
        var content = document.Content ?? document.NormalizedOcrText ?? document.RawOcrText;
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return content.Length <= 120 ? content : content[..120];
    }
}

public sealed class TrackingNotificationService : INotificationService
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

public sealed class RecordingEmailService : IEmailService
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

public sealed class TestPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"test-hash::{password}";

    public bool Verify(string password, string hashedPassword) =>
        string.Equals(Hash(password), hashedPassword, StringComparison.Ordinal);
}

