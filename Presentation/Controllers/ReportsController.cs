using eArchiveSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eArchiveSystem.Presentation.Controllers
{
    [ApiController]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reports;

        public ReportsController(IReportService reports)
        {
            _reports = reports;
        }

        //  عدد الوثائق حسب القسم
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("documents-by-department")]
        public async Task<IActionResult> CountByDepartment()
        {
            var result = await _reports.GetDocumentsCountByDepartmentAsync();
            return Ok(result);
        }

        //  عدد الوثائق حسب النوع
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("documents-by-type")]
        public async Task<IActionResult> CountByType()
        {
            var result = await _reports.GetDocumentsCountByTypeAsync();
            return Ok(result);
        }

        //  نشاط المستخدمين
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("user-activity")]
        public async Task<IActionResult> UserActivity()
        {
            var result = await _reports.GetUserActivityReportAsync();
            return Ok(result);
        }

        //  تقارير زمنية
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("time-report")]
        public async Task<IActionResult> TimeReport()
        {
            var result = await _reports.GetTimeReportAsync();
            return Ok(result);
        }
        // ============================
        // B) EXPORT - DEPARTMENT
        // ============================
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("export/department/excel")]
        public async Task<IActionResult> ExportDeptExcel()
        {
            var file = await _reports.ExportDepartmentReportExcelAsync();
            return File(file,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "DepartmentReport.xlsx");
        }
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("export/department/pdf")]
        public async Task<IActionResult> ExportDeptPdf()
        {
            var file = await _reports.ExportDepartmentReportPdfAsync();
            return File(file,
                "application/pdf",
                "DepartmentReport.pdf");
        }

        // ============================
        // C) EXPORT - TYPE
        // ============================
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("export/type/excel")]
        public async Task<IActionResult> ExportTypeExcel()
        {
            var file = await _reports.ExportTypeReportExcelAsync();
            return File(file,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "DocumentTypeReport.xlsx");
        }
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("export/type/pdf")]
        public async Task<IActionResult> ExportTypePdf()
        {
            var file = await _reports.ExportTypeReportPdfAsync();
            return File(file,
                "application/pdf",
                "DocumentTypeReport.pdf");
        }

        // ============================
        // D) EXPORT - USER ACTIVITY
        // ============================
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("export/user-activity/excel")]
        public async Task<IActionResult> ExportUserExcel()
        {
            var file = await _reports.ExportUserActivityReportExcelAsync();
            return File(file,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "UserActivityReport.xlsx");
        }
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("export/user-activity/pdf")]
        public async Task<IActionResult> ExportUserPdf()
        {
            var file = await _reports.ExportUserActivityReportPdfAsync();
            return File(file,
                "application/pdf",
                "UserActivityReport.pdf");
        }
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("export/all-documents/excel")]
        public async Task<IActionResult> ExportAllDocumentsExcel()
        {
            var file = await _reports.ExportAllDocumentsExcelAsync();

            return File(
                file,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "AllDocuments.xlsx"
            );
        }
    }

}


