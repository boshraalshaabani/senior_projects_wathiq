using eArchiveSystem.Application.Exceptions;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Application.Security;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Services
{
    public class AnalyticsScopeService : IAnalyticsScopeService
    {
        private readonly IUserRepository _users;
        private readonly IDocumentRepository _documents;
        private readonly IAuditRepository _audit;
        private readonly Dictionary<string, User> _actorCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<User>> _scopedUsersCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Document>> _scopedDocumentsCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<AuditLog>> _scopedAuditLogsCache = new(StringComparer.OrdinalIgnoreCase);

        public AnalyticsScopeService(
            IUserRepository users,
            IDocumentRepository documents,
            IAuditRepository audit)
        {
            _users = users;
            _documents = documents;
            _audit = audit;
        }

        public async Task<User> GetActorAsync(string requesterId)
        {
            if (_actorCache.TryGetValue(requesterId, out var cachedActor))
            {
                return cachedActor;
            }

            var actor = await _users.GetByIdAsync(requesterId)
                ?? throw new NotFoundException("User not found");

            _actorCache[requesterId] = actor;
            return actor;
        }

        public async Task<List<User>> GetScopedUsersAsync(string requesterId)
        {
            if (_scopedUsersCache.TryGetValue(requesterId, out var cachedUsers))
            {
                return cachedUsers;
            }

            var actor = await GetActorAsync(requesterId);
            var users = await _users.GetAllAsync();
            List<User> scopedUsers;

            if (ApplicationRoles.IsSystemAdmin(actor.Role))
            {
                scopedUsers = users;
            }
            else if (ApplicationRoles.IsInstitutionAdmin(actor.Role))
            {
                scopedUsers = users
                    .Where(user => SameInstitution(actor.InstitutionId, user.InstitutionId))
                    .Where(user => !ApplicationRoles.IsSystemAdmin(user.Role))
                    .ToList();
            }
            else if (ApplicationRoles.IsManager(actor.Role))
            {
                scopedUsers = users
                    .Where(user => SameInstitution(actor.InstitutionId, user.InstitutionId))
                    .Where(user => SameDepartment(actor.DepartmentId ?? actor.Department, user.DepartmentId ?? user.Department))
                    .Where(user => !ApplicationRoles.IsSystemAdmin(user.Role) && !ApplicationRoles.IsInstitutionAdmin(user.Role))
                    .ToList();
            }
            else
            {
                scopedUsers = users.Where(user => user.Id == actor.Id).ToList();
            }

            _scopedUsersCache[requesterId] = scopedUsers;
            return scopedUsers;
        }

        public async Task<List<Document>> GetScopedDocumentsAsync(string requesterId)
        {
            if (_scopedDocumentsCache.TryGetValue(requesterId, out var cachedDocuments))
            {
                return cachedDocuments;
            }

            var actor = await GetActorAsync(requesterId);
            var documents = await _documents.GetAllAsync();
            List<Document> scopedDocuments;

            if (ApplicationRoles.IsSystemAdmin(actor.Role))
            {
                scopedDocuments = documents;
            }
            else if (ApplicationRoles.IsInstitutionAdmin(actor.Role))
            {
                scopedDocuments = documents
                    .Where(document => SameInstitution(actor.InstitutionId, document.InstitutionId))
                    .ToList();
            }
            else if (ApplicationRoles.IsManager(actor.Role))
            {
                scopedDocuments = documents
                    .Where(document => SameInstitution(actor.InstitutionId, document.InstitutionId))
                    .Where(document => SameDepartment(actor.DepartmentId ?? actor.Department, document.DepartmentId ?? document.Department))
                    .ToList();
            }
            else
            {
                scopedDocuments = documents.Where(document => document.UserId == actor.Id).ToList();
            }

            _scopedDocumentsCache[requesterId] = scopedDocuments;
            return scopedDocuments;
        }

        public async Task<List<AuditLog>> GetScopedAuditLogsAsync(string requesterId)
        {
            if (_scopedAuditLogsCache.TryGetValue(requesterId, out var cachedLogs))
            {
                return cachedLogs;
            }

            var actor = await GetActorAsync(requesterId);
            var logs = await _audit.GetAllAsync();
            List<AuditLog> scopedLogs;

            if (ApplicationRoles.IsSystemAdmin(actor.Role))
            {
                scopedLogs = logs;
            }
            else
            {
                var scopedUsers = await GetScopedUsersAsync(requesterId);
                var scopedDocuments = await GetScopedDocumentsAsync(requesterId);

                var allowedUserIds = scopedUsers
                    .Select(user => user.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var allowedDocumentIds = scopedDocuments
                    .Select(document => document.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                scopedLogs = logs
                    .Where(log =>
                        (!string.IsNullOrWhiteSpace(log.DocumentId) && allowedDocumentIds.Contains(log.DocumentId)) ||
                        (!string.IsNullOrWhiteSpace(log.UserId) && allowedUserIds.Contains(log.UserId)))
                    .ToList();
            }

            _scopedAuditLogsCache[requesterId] = scopedLogs;
            return scopedLogs;
        }

        private static bool SameInstitution(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameDepartment(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
