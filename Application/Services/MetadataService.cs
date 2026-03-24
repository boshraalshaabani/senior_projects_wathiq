using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Services
{ 
    public class MetadataService : IMetadataService
    {
        private readonly IDocumentRepository _documents;
        private readonly IMetadataRepository _metadata;
        private readonly IAuditService _audit;
        private readonly IIndexingService _indexing;

        public MetadataService(
            IDocumentRepository documents,
            IMetadataRepository metadata,
            IAuditService audit,
            IIndexingService indexing)
        {
            _documents = documents;
            _metadata = metadata;
            _audit = audit;
            _indexing = indexing;
        }

        private bool CanEdit(Document doc, string userId, string role)
        {
            // Admin لا يعدّل أبداً
            if (role == "Admin")
                return false;

            // Manager يعدّل دائماً
            if (role == "Manager")
                return true;

            // User يعدّل فقط لو يملك الوثيقة
            if (role == "User" && doc.UserId == userId)
                return true;

            return false;
        }

        // صلاحية العرض
        private bool CanView(Document doc, string userId, string role)
        {
            if (role == "Admin" || role == "Manager")
                return true;

            if (role == "User" && doc.UserId == userId)
                return true;

            return false;
        }

        // ---------------------------------------
        // ADD METADATA
        // ---------------------------------------

        // إضافة Metadata لأول مرة
        public async Task<bool> AddMetadataAsync(string documentId, AddMetadataDto dto, string userId, string role)
        {

            // 1) جلب الوثيقة
            var doc = await _documents.GetByIdAsync(documentId);
            if (doc == null)
                return false;

            if (!CanEdit(doc, userId, role))
                return false;

            // 2) إنشاء Metadata
            var meta = new Metadata
            {
                Id = documentId,
                Description = dto.Description,
                Category = dto.Category,
                Tags = dto.Tags,
                Department = dto.Department,
                DocumentType = dto.DocumentType,
                ExpirationDate = dto.ExpirationDate,
                CreatedAt = DateTime.UtcNow
            };

            // 3) حفظ Metadata
            await _metadata.UpsertAsync(meta);

            // 4) ربطها مع Document
            doc.Metadata = meta;
            doc.Department = dto.Department;
            doc.UpdatedAt = DateTime.UtcNow;
            await _documents.UpdateAsync(doc.Id, doc);
            await _indexing.SyncDocumentAsync(documentId);


            // 5) Audit
            await _audit.LogAsync(
                userId,
                role,
                "AddMetadata",
                documentId,
                $"User {userId} added metadata to document {documentId}"
            );

            return true;
        }

        // ---------------------------------------
        // VIEW METADATA
        // ---------------------------------------

        // عرض Metadata
        public async Task<Metadata?> ViewMetadataAsync(string documentId, string userId, string role)
        {

            // 1) جلب الوثيقة
            var doc = await _documents.GetByIdAsync(documentId);
            if (doc == null)
                return null;

            if (!CanView(doc, userId, role))
                return null;


            // 2) جلب Metadata
            var meta = await _metadata.GetByDocumentIdAsync(documentId);

            // 3) Audit
            await _audit.LogAsync(
                userId,
                role,
                "ViewMetadata",
                documentId,
                $"User {userId} viewed metadata for document {documentId}"
            );

            return meta;
        }

        // ---------------------------------------
        // UPDATE METADATA
        // ---------------------------------------

        // تعديل Metadata (أو إنشاؤها إن لم تكن موجودة)
        public async Task<bool> UpdateMetadataAsync(string documentId, AddMetadataDto dto, string userId, string role)
        {

            // 1) جلب الوثيقة
            var doc = await _documents.GetByIdAsync(documentId);
            if (doc == null)
                return false;

            if (!CanEdit(doc, userId, role))
                return false;

            // 2) جلب Metadata الحالية
            var existing = await _metadata.GetByDocumentIdAsync(documentId);

            // ----------------------------------
            // CASE 1: Metadata غير موجودة
            // ----------------------------------

            if (existing == null)
            {
                var meta = new Metadata
                {
                    Id = documentId,
                    Description = dto.Description,
                    Category = dto.Category,
                    Tags = dto.Tags,
                    Department = dto.Department,
                    DocumentType = dto.DocumentType,
                    ExpirationDate = dto.ExpirationDate,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _metadata.UpsertAsync(meta);

                doc.Metadata = meta;
                doc.UpdatedAt = DateTime.UtcNow;
                doc.Department = dto.Department;
                await _documents.UpdateAsync(doc.Id, doc);
                await _indexing.SyncDocumentAsync(documentId);

                // Audit
                await _audit.LogAsync(
                    userId,
                    role,
                    "AddMetadata",
                    documentId,
                    $"User {userId} added metadata to document {documentId}"
                );

                return true;
            }

            // ----------------------------------
            // CASE 2: Metadata موجودة → تعديل
            // ----------------------------------
            existing.Description = dto.Description;
            existing.Category = dto.Category;
            existing.Tags = dto.Tags;
            existing.Department = dto.Department;
            existing.DocumentType = dto.DocumentType;
            existing.ExpirationDate = dto.ExpirationDate;
            existing.UpdatedAt = DateTime.UtcNow;

            await _metadata.UpsertAsync(existing); 

            doc.Metadata = existing;
            doc.UpdatedAt = DateTime.UtcNow;
            doc.Department = dto.Department;

            await _documents.UpdateAsync(doc.Id, doc);
            await _indexing.SyncDocumentAsync(documentId);

            //  Audit
            await _audit.LogAsync(
                userId,
                role,
                "UpdateMetadata",
                documentId,
                $"User {userId} updated metadata for document {documentId}"
            );

            return true;
        }
    }

}
    


