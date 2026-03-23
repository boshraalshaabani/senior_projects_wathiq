using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace eArchiveSystem.Domain.Models
{
    [BsonIgnoreExtraElements]
    public class Document
        { 
            // MongoDB ObjectId
            [BsonId]
            [BsonRepresentation(BsonType.ObjectId)]
            public string Id { get; set; }

            // Document title
            public string Title { get; set; }

            // OCR extracted text (optional)
            public string? Content { get; set; }

            // File path on the server
            public string FilePath { get; set; }

            // Original file name
            public string FileName { get; set; }

            // File MIME type (e.g. application/pdf)
            public string ContentType { get; set; }

            // File size in bytes
            public long Size { get; set; }

            // File hash (duplicate detection)
            public string FileHash { get; set; }

            // Creation & last update timestamps
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }

            // Owner user ID
            public string UserId { get; set; }

            // Department the document belongs to
            public string Department { get; set; }

            // Embedded metadata (synced with Metadata collection)
            [BsonElement("Metadata")]
            public Metadata? Metadata { get; set; }

            // Owner name (for view only – not stored in DB)
            [BsonIgnore]
            public string? OwnerName { get; set; }
        }
    }

