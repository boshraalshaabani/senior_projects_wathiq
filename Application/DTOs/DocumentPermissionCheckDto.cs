namespace eArchiveSystem.Application.DTOs
{
    public class DocumentPermissionCheckDto
    {
        public string DocumentId { get; set; } = string.Empty;
        public string OwnerUserId { get; set; } = string.Empty;
        public string? InstitutionId { get; set; }
        public string? DepartmentId { get; set; }
        public string? Department { get; set; }
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
