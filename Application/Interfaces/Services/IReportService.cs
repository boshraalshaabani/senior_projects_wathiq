using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Interfaces.Services
{
    public interface IReportService
    {
        Task<Dictionary<string, int>> GetDocumentsCountByDepartmentAsync();
        Task<Dictionary<string, int>> GetDocumentsCountByTypeAsync();
        Task<List<Report>> GetUserActivityReportAsync();
        Task<object> GetTimeReportAsync();
       
        // Export - Department
        Task<byte[]> ExportDepartmentReportPdfAsync();
        Task<byte[]> ExportDepartmentReportExcelAsync();
        
        // Export - Type
        Task<byte[]> ExportTypeReportExcelAsync();
        Task<byte[]> ExportTypeReportPdfAsync();

        // Export - User Activity
        Task<byte[]> ExportUserActivityReportExcelAsync();
        Task<byte[]> ExportUserActivityReportPdfAsync();
        Task<byte[]> ExportAllDocumentsExcelAsync();


    }
}
