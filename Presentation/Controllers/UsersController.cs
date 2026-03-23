using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace eArchiveSystem.Presentation.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;

        public UsersController(IUserService service)
        {
            _service = service;
        }

        // ADD USER (Admin only)
        [Authorize(Roles = "Admin")]
        [HttpPost("add")]
        public async Task<IActionResult> AddUser(AddUserDto dto)
        {
            var user = await _service.AddUser(dto);
            return Ok(user);
        }


        [Authorize(Roles = "Admin")]
        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdmin(CreateAdminDto dto)
        {
            var admin = await _service.CreateAdmin(dto);
            return Ok(new
            {
                message = "Admin created successfully",
                admin = new
                {
                    id = admin.Id,
                    name = admin.Name,
                    email = admin.Email,
                    role = admin.Role
                }
            });
        }

        [Authorize]   // أي مستخدم مسجل دخول يقدر يعدل نفسه
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var result = await _service.UpdateProfile(userId, dto);

            return Ok(new { message = result });
        }

        //Update Role (assign-role) 

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/assign-role")]
        public async Task<IActionResult> AssignRole(string id, AssignRoleDto dto)
        {
            var result = await _service.AssignRole(id, dto.Role);
            return Ok(new { message = result });
        }


        // DELETE USER (Admin only)
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var result = await _service.DeleteUser(id, role);

            return Ok(new { message = result });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("edit/{id}")]
        public async Task<IActionResult> EditUser(string id, UpdateUserDto dto)
        {
            var result = await _service.EditUser(id, dto);
            return Ok(new { message = result });
        }
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] string? role, [FromQuery] string? search)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var users = await _service.GetUsers(role, search, currentUserId);
            return Ok(users);
        }

        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var result = await _service.ChangePassword(userId, dto);

            return Ok(new { message = result });
        }

        // =========================
        // Two-Factor Authentication (2FA)
        // =========================

        /// <summary>
        /// Get current user's 2FA status.
        /// </summary>
        [Authorize]
        [HttpGet("2fa")]
        public async Task<IActionResult> Get2FAStatus()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var enabled = await _service.GetTwoFactorEnabled(userId);
            return Ok(new { enabled });
        }

        /// <summary>
        /// Enable/disable current user's 2FA.
        /// </summary>
        [Authorize]
        [HttpPut("2fa")]
        public async Task<IActionResult> Set2FAStatus([FromBody] TwoFactorToggleDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var message = await _service.SetTwoFactorEnabled(userId, dto.Enabled);
            return Ok(new { message, enabled = dto.Enabled });
        }

    }
} 
