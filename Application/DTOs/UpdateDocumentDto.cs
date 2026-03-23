namespace eArchiveSystem.Application.DTOs
{
    public class UpdateDocumentDto
    {
        public string? Title { get; set; }
        public IFormFile? File { get; set; }   // الملف الجديد (اختياري)
    }
}
