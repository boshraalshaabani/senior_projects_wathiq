namespace eArchiveSystem.IntegrationTests.Infrastructure;

public sealed class IntegrationTestState
{
    public Dictionary<string, User> Users { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Document> Documents { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Metadata> Metadata { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Department> Departments { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> IndexedDocumentIds { get; } = new();
    public List<string> ApprovedDocumentIds { get; } = new();
    public List<string> RejectedDocumentIds { get; } = new();
    public List<string> TransferredDocumentIds { get; } = new();
    public List<(string UserId, string Action, string? DocumentId, string Description)> AuditEntries { get; } = new();
    public List<(string To, string Subject, string Body)> SentEmails { get; } = new();
}
