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

        // حاليًا الوصول محصور بالأدوار التي كانت تستعمل هذه الشاشة سابقًا
        [Authorize(Roles = "SystemAdmin,InstitutionAdmin,Manager")]
        [HttpGet]
        public async Task<IActionResult> GetAuditLogs()
        {
            var requesterId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
            var logs = await _audit.GetAllWithUsersAsync(requesterId);
            return Ok(logs);
        }
    }
}
