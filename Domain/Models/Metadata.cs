using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace eArchiveSystem.Domain.Models
{
        public class Metadata
        {
            // Same ID as the related Document (1:1 relation)
            [BsonId]
            [BsonRepresentation(BsonType.ObjectId)]
            public string Id { get; set; } = default!;

            // Short description / summary
            public string? Description { get; set; }

            // Document category 
            public string? Category { get; set; }

            // Document type (e.g. PDF, Report)
            public string? DocumentType { get; set; }

            // Searchable tags
            public List<string>? Tags { get; set; }

            // Metadata creation timestamp
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

            // Last metadata update timestamp
            public DateTime? UpdatedAt { get; set; }

            // Department associated with the document
            public string? Department { get; set; }

            // Optional expiration date
            public DateTime? ExpirationDate { get; set; }
        }
    }

