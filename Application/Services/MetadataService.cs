using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Exceptions;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Application.Security;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Services
{
    public class MetadataService : IMetadataService
    {
        private readonly IDocumentRepository _documents;
        private readonly IMetadataRepository _metadata;
        private readonly IDepartmentRepository _departments;
        private readonly IUserRepository _users;
        private readonly IAuditService _audit;
        private readonly IIndexingService _indexing;
        private readonly IDocumentAuthorizationService _authorization;

        public MetadataService(
            IDocumentRepository documents,
            IMetadataRepository metadata,
            IDepartmentRepository departments,
            IUserRepository users,
            IAuditService audit,
            IIndexingService indexing,
            IDocumentAuthorizationService authorization)
        {
            _documents = documents;
            _metadata = metadata;
            _departments = departments;
            _users = users;
            _audit = audit;
            _indexing = indexing;
            _authorization = authorization;
        }

        public async Task<bool> AddMetadataAsync(string documentId, AddMetadataDto dto, string userId, string role)
        {
            var doc = await _documents.GetByIdAsync(documentId);
            if (doc == null)
                return false;

            var actor = await _users.GetByIdAsync(userId);
            if (actor == null || !_authorization.CanEdit(actor, doc))
                return false;

            var departmentAssignment = await ResolveDepartmentAssignmentAsync(actor, doc, dto);

            var meta = new Metadata
            {
                Id = documentId,
                Description = dto.Description,
                Category = dto.Category,
                Tags = dto.Tags,
                IssuingEntity = dto.IssuingEntity,
                ReferenceNumber = dto.ReferenceNumber,
                DocumentDate = dto.DocumentDate,
                Insights = dto.Insights,
                HasSignature = dto.HasSignature,
                Signatures = dto.Signatures,
                Headers = dto.Headers,
                Footers = dto.Footers,
                Stamps = dto.Stamps,
                RawExtractionJson = dto.RawExtractionJson,
                Department = departmentAssignment.DepartmentName,
                DepartmentId = departmentAssignment.DepartmentId,
                DocumentType = dto.DocumentType,
                ExpirationDate = dto.ExpirationDate,
                CreatedAt = DateTime.UtcNow
            };

            await _metadata.UpsertAsync(meta);

            doc.Metadata = meta;
            doc.Department = departmentAssignment.DepartmentName;
            doc.DepartmentId = departmentAssignment.DepartmentId;
            doc.UpdatedAt = DateTime.UtcNow;

            await _documents.UpdateAsync(doc.Id, doc);
            await _indexing.SyncDocumentAsync(documentId);

            await _audit.LogAsync(
                userId,
                role,
                "AddMetadata",
                documentId,
                $"User {userId} added metadata to document {documentId}");

            return true;
        }

        public async Task<Metadata?> ViewMetadataAsync(string documentId, string userId, string role)
        {
            var doc = await _documents.GetByIdAsync(documentId);
            if (doc == null)
                return null;

            var actor = await _users.GetByIdAsync(userId);
            if (actor == null || !_authorization.CanView(actor, doc))
                return null;

            var meta = await _metadata.GetByDocumentIdAsync(documentId);

            await _audit.LogAsync(
                userId,
                role,
                "ViewMetadata",
                documentId,
                $"User {userId} viewed metadata for document {documentId}");

            return meta;
        }

        public async Task<bool> UpdateMetadataAsync(string documentId, AddMetadataDto dto, string userId, string role)
        {
            var doc = await _documents.GetByIdAsync(documentId);
            if (doc == null)
                return false;

            var actor = await _users.GetByIdAsync(userId);
            if (actor == null || !_authorization.CanEdit(actor, doc))
                return false;

            var departmentAssignment = await ResolveDepartmentAssignmentAsync(actor, doc, dto);
            var existing = await _metadata.GetByDocumentIdAsync(documentId);

            if (existing == null)
            {
                var meta = new Metadata
                {
                    Id = documentId,
                    Description = dto.Description,
                    Category = dto.Category,
                    Tags = dto.Tags,
                    IssuingEntity = dto.IssuingEntity,
                    ReferenceNumber = dto.ReferenceNumber,
                    DocumentDate = dto.DocumentDate,
                    Insights = dto.Insights,
                    HasSignature = dto.HasSignature,
                    Signatures = dto.Signatures,
                    Headers = dto.Headers,
                    Footers = dto.Footers,
                    Stamps = dto.Stamps,
                    RawExtractionJson = dto.RawExtractionJson,
                    Department = departmentAssignment.DepartmentName,
                    DepartmentId = departmentAssignment.DepartmentId,
                    DocumentType = dto.DocumentType,
                    ExpirationDate = dto.ExpirationDate,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _metadata.UpsertAsync(meta);

                doc.Metadata = meta;
                doc.UpdatedAt = DateTime.UtcNow;
                doc.Department = departmentAssignment.DepartmentName;
                doc.DepartmentId = departmentAssignment.DepartmentId;

                await _documents.UpdateAsync(doc.Id, doc);
                await _indexing.SyncDocumentAsync(documentId);

                await _audit.LogAsync(
                    userId,
                    role,
                    "AddMetadata",
                    documentId,
                    $"User {userId} added metadata to document {documentId}");

                return true;
            }

            existing.Description = dto.Description;
            existing.Category = dto.Category;
            existing.Tags = dto.Tags;
            existing.IssuingEntity = dto.IssuingEntity;
            existing.ReferenceNumber = dto.ReferenceNumber;
            existing.DocumentDate = dto.DocumentDate;
            existing.Insights = dto.Insights;
            existing.HasSignature = dto.HasSignature;
            existing.Signatures = dto.Signatures;
            existing.Headers = dto.Headers;
            existing.Footers = dto.Footers;
            existing.Stamps = dto.Stamps;
            existing.RawExtractionJson = dto.RawExtractionJson;
            existing.Department = departmentAssignment.DepartmentName;
            existing.DepartmentId = departmentAssignment.DepartmentId;
            existing.DocumentType = dto.DocumentType;
            existing.ExpirationDate = dto.ExpirationDate;
            existing.UpdatedAt = DateTime.UtcNow;

            await _metadata.UpsertAsync(existing);

            doc.Metadata = existing;
            doc.UpdatedAt = DateTime.UtcNow;
            doc.Department = departmentAssignment.DepartmentName;
            doc.DepartmentId = departmentAssignment.DepartmentId;

            await _documents.UpdateAsync(doc.Id, doc);
            await _indexing.SyncDocumentAsync(documentId);

            await _audit.LogAsync(
                userId,
                role,
                "UpdateMetadata",
                documentId,
                $"User {userId} updated metadata for document {documentId}");

            return true;
        }

        private async Task<(string? DepartmentId, string? DepartmentName)> ResolveDepartmentAssignmentAsync(
            User actor,
            Document document,
            AddMetadataDto dto)
        {
            var requestedDepartmentId = dto.DepartmentId?.Trim();
            var requestedDepartmentName = dto.Department?.Trim();

            if (string.IsNullOrWhiteSpace(requestedDepartmentId))
            {
                if (string.IsNullOrWhiteSpace(requestedDepartmentName) ||
                    string.Equals(requestedDepartmentName, document.Department, StringComparison.OrdinalIgnoreCase))
                {
                    return (document.DepartmentId, document.Department);
                }

                throw new ValidationException("DepartmentId is required when changing the document department");
            }

            var department = await _departments.GetByIdAsync(requestedDepartmentId)
                ?? throw new NotFoundException("Department not found");

            if (!string.Equals(department.InstitutionId, document.InstitutionId, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Department does not belong to the document institution");

            if (ApplicationRoles.IsInstitutionAdmin(actor.Role) &&
                !string.Equals(actor.InstitutionId, department.InstitutionId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedActionException("You can only assign departments within your institution");
            }

            if (ApplicationRoles.IsManager(actor.Role))
            {
                var actorDepartmentId = actor.DepartmentId ?? actor.Department;
                if (!string.Equals(actorDepartmentId, department.Id, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedActionException("Manager can only assign documents to their own department");
            }

            if (!string.IsNullOrWhiteSpace(requestedDepartmentName) &&
                !string.Equals(requestedDepartmentName, department.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("Department name does not match the selected DepartmentId");
            }

            return (department.Id, department.Name);
        }
    }
}
