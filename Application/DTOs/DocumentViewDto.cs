using eArchiveSystem.Domain.Models;
namespace eArchiveSystem.Application.DTOs
{
    public class DocumentViewDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Department { get; set; }
        public string OwnerName { get; set; }
        public string OwnerEmail { get; set; }
        public Metadata Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        

    }
}
