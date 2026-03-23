using Microsoft.AspNetCore.Http;

namespace eArchiveSystem.Application.DTOs
{
    public class AddDocumentDto
    {
        public string? Title { get; set; }
        public IFormFile File { get; set; }
        public string? TargetUserId { get; set; }
        public bool EnableOcr { get; set; }


    }
}

      