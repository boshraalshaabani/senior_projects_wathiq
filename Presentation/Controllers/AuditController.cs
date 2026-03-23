using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using eArchiveSystem.Application.Interfaces.Services;

namespace eArchiveSystem.Presentation.Controllers
{
    [ApiController]
    [Route("api/audit")]

    public class AuditController : ControllerBase
    {
        private readonly IAuditService _audit;

        public AuditController(IAuditService audit)
        {
            _audit = audit;
        }

        // فقط الـ Admin يقدر يشوف السجلّات
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet]
        public async Task<IActionResult> GetAuditLogs()
        {
            var logs = await _audit.GetAllWithUsersAsync();
            return Ok(logs);
        }
    }
}
