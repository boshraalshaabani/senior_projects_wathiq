namespace eArchiveSystem.Application.DTOs
{

    public class UpdateMetadataDto
    {
        public string? Description { get; set; }
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
        public string? DocumentType { get; set; }
        public DateTime? ExpirationDate { get; set; }
    }
}