namespace eArchiveSystem.Application.DTOs
{
    public class OcrCallbackDto
    {
        public string Text { get; set; } = string.Empty;
        public OcrStructuredDataDto? StructuredData { get; set; }
        public string? RawJson { get; set; }
    }
}
