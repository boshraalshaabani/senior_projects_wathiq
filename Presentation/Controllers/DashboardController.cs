using eArchiveSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eArchiveSystem.Presentation.Controllers
{

        [ApiController]
        [Route("api/dashboard")]
        [Authorize(Roles = "Admin,Manager")]
        public class DashboardController : ControllerBase
        {
            private readonly IDashboardService _dashboard;

            public DashboardController(IDashboardService dashboard)
            {
                _dashboard = dashboard;
            }

            [HttpGet("totals")]
            public async Task<IActionResult> GetTotals()
            {
                var result = new
                {
                    totalDocuments = await _dashboard.GetTotalDocumentsAsync(),
                    totalUsers = await _dashboard.GetTotalUsersAsync(),
                    todayUploads = await _dashboard.GetTodayUploadsAsync(),
                    monthlyUpdates = await _dashboard.GetMonthlyUpdatesAsync()
                };

                return Ok(result);
            }

            [HttpGet("documents-by-department")]
            public async Task<IActionResult> DocumentsByDepartment()
            {
                return Ok(await _dashboard.GetDocumentsByDepartmentAsync());
            }

            [HttpGet("documents-by-type")]
            public async Task<IActionResult> DocumentsByType()
            {
                return Ok(await _dashboard.GetDocumentsByTypeAsync());
            }
        }
    }



