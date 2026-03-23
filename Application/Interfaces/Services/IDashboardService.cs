namespace eArchiveSystem.Application.Interfaces.Services
{
    public interface IDashboardService
    {
        Task<int> GetTotalDocumentsAsync();
        Task<int> GetTotalUsersAsync();
        Task<int> GetTodayUploadsAsync();
        Task<int> GetMonthlyUpdatesAsync();

        Task<Dictionary<string, int>> GetDocumentsByDepartmentAsync();
        Task<Dictionary<string, int>> GetDocumentsByTypeAsync();


    }
}
