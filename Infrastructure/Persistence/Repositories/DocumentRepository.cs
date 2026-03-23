using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Domain.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace eArchiveSystem.Infrastructure.Persistence.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        // Collection الأساسية للوثائق
        private readonly IMongoCollection<Document> _documents;
        // Collection الخاصة بالـ Metadata (مستقلة)
        private readonly IMongoCollection<Metadata> _metadata;

        public DocumentRepository(IMongoDatabase database)
        {
            _documents = database.GetCollection<Document>("Documents");
            _metadata = database.GetCollection<Metadata>("Metadata");
        }

        // =========================================================
        // CREATE
        // =========================================================
        public async Task CreateAsync(Document document)
        {
            await _documents.InsertOneAsync(document);
        }

        // =========================================================
        // GET
        // =========================================================
        public async Task<Document?> GetByIdAsync(string id)
        {
            return await _documents
                .Find(d => d.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<Document> GetByHashAsync(string fileHash)
        {
            return await _documents
                .Find(d => d.FileHash == fileHash)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Document>> GetByUserAsync(string userId)
        {
            return await _documents
                .Find(d => d.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<Document>> GetAllAsync()
        {
            return await _documents
                .Find(_ => true)
                .ToListAsync();
        }

        // =========================================================
        // UPDATE (FULL) استخدمها فقط عندما تكون الوثيقة كاملة
        // =========================================================
        public async Task UpdateAsync(string id, Document document)
        {
            
            // هذه الدالة تستبدل الوثيقة بالكامل
            // لا تستخدمها بعد OCR أو بعد AttachMetadataAsync
            await _documents.ReplaceOneAsync(
                d => d.Id == id,
                document
            );
        }

        // =========================================================
        // UPDATE (PARTIAL) – OCR Content فقط
        // =========================================================
        public async Task UpdateContentAsync(
            string documentId,
            string content,
            string department
        )
        {
            var update = Builders<Document>.Update
                .Set(d => d.Content, content)
                .Set(d => d.Department, department)
                .Set(d => d.UpdatedAt, DateTime.UtcNow);

            await _documents.UpdateOneAsync(
                d => d.Id == documentId,
                update
            );
        }

        // =========================================================
        // METADATA – Embed كامل داخل Document
        // =========================================================
        public async Task AttachMetadataAsync(string documentId)
        {
            // نجلب الميتاداتا من collection الخاصة بها
            var metadata = await _metadata
                .Find(m => m.Id == documentId)
                .FirstOrDefaultAsync();

            if (metadata == null)
                return;

            var update = Builders<Document>.Update
                .Set(d => d.Metadata, metadata)
                .Set(d => d.UpdatedAt, DateTime.UtcNow);

            await _documents.UpdateOneAsync(
                d => d.Id == documentId,
                update
            );
        }

        // =========================================================
        // METADATA – Update الحقول فقط (بدون استبدال)
        // =========================================================
        public async Task UpdateMetadataFieldsAsync(
            string documentId,
            Metadata metadata
        )
        {
            var update = Builders<Document>.Update
                .Set("Metadata.Description", metadata.Description)
                .Set("Metadata.Category", metadata.Category)
                .Set("Metadata.DocumentType", metadata.DocumentType)
                .Set("Metadata.Tags", metadata.Tags)
                .Set("Metadata.Department", metadata.Department)
                .Set("Metadata.ExpirationDate", metadata.ExpirationDate)
                .Set("Metadata.CreatedAt", metadata.CreatedAt)
                .Set("Metadata.UpdatedAt", metadata.UpdatedAt)
                .Set(d => d.UpdatedAt, DateTime.UtcNow);

            await _documents.UpdateOneAsync(
                d => d.Id == documentId,
                update
            );
        }

        // =========================================================
        // DELETE
        // =========================================================
        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _documents.DeleteOneAsync(d => d.Id == id);
            return result.DeletedCount > 0;
        }

        // =========================================================
        // SEARCH (كما هو – بدون تغيير)
        // =========================================================
        public async Task<List<Document>> SearchAsync(
            SearchDocumentsDto dto,
            string userId,
            string role
        )
        {
            var filters = new List<FilterDefinition<Document>>();

            if (role == "User")
                filters.Add(Builders<Document>.Filter.Eq(d => d.UserId, userId));

            if (!string.IsNullOrEmpty(dto.Query))
            {
                var text = new BsonRegularExpression(dto.Query, "i");

                filters.Add(
                    Builders<Document>.Filter.Or(
                        Builders<Document>.Filter.Regex(d => d.Title, text),
                        Builders<Document>.Filter.Regex("Metadata.Description", text),
                        Builders<Document>.Filter.Regex("Metadata.Tags", text),
                        Builders<Document>.Filter.Regex("Metadata.Category", text),
                        Builders<Document>.Filter.Regex("Metadata.DocumentType", text)
                    )
                );
            }

            if (!string.IsNullOrEmpty(dto.Category))
                filters.Add(
                    Builders<Document>.Filter.Eq(
                        d => d.Metadata.Category,
                        dto.Category
                    )
                );

            if (!string.IsNullOrEmpty(dto.Department))
                filters.Add(
                    Builders<Document>.Filter.Eq(
                        d => d.Department,
                        dto.Department
                    )
                );

            if (dto.FromDate != null)
                filters.Add(
                    Builders<Document>.Filter.Gte(d => d.CreatedAt, dto.FromDate)
                );

            if (dto.ToDate != null)
                filters.Add(
                    Builders<Document>.Filter.Lte(d => d.CreatedAt, dto.ToDate)
                );

            var finalFilter = filters.Count > 0
                ? Builders<Document>.Filter.And(filters)
                : Builders<Document>.Filter.Empty;

            var sort = dto.SortBy switch
            {
                "Title" => dto.Desc
                    ? Builders<Document>.Sort.Descending(d => d.Title)
                    : Builders<Document>.Sort.Ascending(d => d.Title),

                "CreatedAt" => dto.Desc
                    ? Builders<Document>.Sort.Descending(d => d.CreatedAt)
                    : Builders<Document>.Sort.Ascending(d => d.CreatedAt),

                _ => Builders<Document>.Sort.Descending(d => d.CreatedAt)
            };

            return await _documents
                .Find(finalFilter)
                .Sort(sort)
                .ToListAsync();
        }
    }
}
