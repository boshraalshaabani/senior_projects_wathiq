namespace eArchiveSystem.Domain.Models
{
    public class AuditLog
    {
        public string Id { get; set; }

        public DateTime Timestamp { get; set; }  // وقت الحدث

        public string UserId { get; set; }       // مين قام بالعملية
        public string UserRole { get; set; }     // دوره وقت تنفيذ الحدث

        public string Action { get; set; }       // نوع الحدث (AddDocument, DeleteDocument…)

        public string DocumentId { get; set; }   // إذا الحدث متعلق بوثيقة
        public string Description { get; set; }  // نص يشرح الحدث (مثال: "User X deleted document Y")

        
    }
}
