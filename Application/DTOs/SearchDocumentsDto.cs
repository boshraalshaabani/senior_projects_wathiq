namespace eArchiveSystem.Application.DTOs
{
    public class SearchDocumentsDto
    {
        public string? Query { get; set; }       // النص الذي سيتم البحث عنه
        public string? Category { get; set; }   // فلتر اختياري
        public string? Department { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; } 
        public string? SortBy { get; set; }     // Title, CreatedAt, etc.
        public bool Desc { get; set; } = false; // ترتيب عكسي
    }
}
