using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Services;

namespace eArchiveSystem.Application.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDocumentRepository _documents;
        private readonly IUserRepository _users;
        private readonly IAuditRepository _audit;

        public DashboardService(
            IDocumentRepository documents,
            IUserRepository users,
            IAuditRepository audit)
        {
            _documents = documents;
            _users = users;
            _audit = audit;
        }

        public async Task<int> GetTotalDocumentsAsync()
        {
            var docs = await _documents.GetAllAsync();
            return docs.Count;
        }

        public async Task<int> GetTotalUsersAsync()
        {
            var users = await _users.GetAllAsync();
            return users.Count;
        }

        public async Task<int> GetTodayUploadsAsync()
        {
            var logs = await _audit.GetAllAsync();
            var today = DateTime.UtcNow.Date;

            return logs.Count(l =>
                l.Action == "AddDocument" &&
                l.Timestamp.Date == today);
        }

        public async Task<int> GetMonthlyUpdatesAsync()
        {
            var logs = await _audit.GetAllAsync();
            var now = DateTime.UtcNow;

            return logs.Count(l =>
                l.Action == "UpdateDocument" &&
                l.Timestamp.Month == now.Month &&
                l.Timestamp.Year == now.Year);
        }

        public async Task<Dictionary<string, int>> GetDocumentsByDepartmentAsync()
        {
            var docs = await _documents.GetAllAsync();

            return docs
                .GroupBy(d => d.Department ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<Dictionary<string, int>> GetDocumentsByTypeAsync()
        {
            var docs = await _documents.GetAllAsync();

            return docs
                .Where(d => d.Metadata != null)
                .GroupBy(d => d.Metadata.DocumentType ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}

