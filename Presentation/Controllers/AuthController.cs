using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Services;

using Microsoft.AspNetCore.Mvc;

namespace eArchiveSystem.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService)
        {
            _userService = userService;
        }

        // LOGIN
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var result = await _userService.Login(dto);

            // الحالة الأولى: نحتاج تأكيد 2FA
            if (result.Requires2FA)
            {
                return Ok(new
                {
                    requires2FA = true,
                    message = result.Message
                });
            }

            // الحالة الثانية: تسجيل دخول كامل (تم تجاوز 2FA)
            return Ok(new
            {
                token = result.Token,
                user = new
                {
                    id = result.User.Id,
                    name = result.User.Name,
                    email = result.User.Email,
                    role = result.User.Role
                },
                requires2FA = false
            });
        }


        // LOGOUT
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var message = await _userService.Logout();
            return Ok(new { message });
        }

        // FORGOT PASSWORD
        [HttpPost("password/forgot")]
        public async Task<IActionResult> Forgot(ForgotPasswordDto dto)
        {
            var result = await _userService.ForgotPassword(dto);
            return Ok(result);
        }


        // RESET PASSWORD
        [HttpPost("password/reset")]
        public async Task<IActionResult> Reset(ResetPasswordDto dto)
        {
            var result = await _userService.ResetPassword(dto);
            return Ok(result);
        }

        [HttpPost("verify-2fa")]
        public async Task<IActionResult> Verify2FA([FromBody] Verify2FADto dto)
        {
            var result = await _userService.VerifyTwoFactorAsync(dto);

            return Ok(new
            {
                token = result.Token,
                user = new
                {
                    id = result.User.Id,
                    name = result.User.Name,
                    email = result.User.Email,
                    role = result.User.Role
                },
                requires2FA = false,
                message = result.Message
            });
        }


    }
}
