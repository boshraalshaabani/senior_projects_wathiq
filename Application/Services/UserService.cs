using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Security;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Domain.Models;
using eArchiveSystem.Utils;

namespace eArchiveSystem.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly IPasswordHasher _hasher;
        private readonly ITokenService _token;
        private readonly IEmailService _email;
        private readonly IConfiguration _config;
        private readonly IAuditService _audit;



        public UserService(
      IUserRepository repo,
      IPasswordHasher hasher,
      ITokenService token,
      IEmailService email,
      IConfiguration config,
      IAuditService audit
  )
        {
            _repo = repo;
            _hasher = hasher;
            _token = token;
            _email = email;
            _config = config;
            _audit = audit;
        }


        // ADD USER  (Instead of Register)
        public async Task<User> AddUser(AddUserDto dto)
        {
            var exists = await _repo.GetByEmailAsync(dto.Email);
            if (exists != null)
                throw new Exception("Email already used");

            string hashedPassword = _hasher.Hash(dto.Password);

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Password = hashedPassword,
                Role = dto.Role,
                Department = dto.Department
            };
            await _repo.CreateAsync(user);
            return user;
        }

        // LOGIN
      

        public async Task<AuthResult> Login(LoginDto dto)
        {
            var user = await _repo.GetByEmailAsync(dto.Email);
            if (user == null)
            {
                await _audit.LogAsync(
                    userId: "null",
                    role: "None",
                    action: "LoginFailed",
                    documentId: null,
                    description: $"Failed login attempt for email {dto.Email}"
                );

                throw new Exception("Invalid email or password");
            }


            // 1) Check if user is locked
            if (user.LockoutUntil != null && user.LockoutUntil > DateTime.UtcNow)
            {
                var remainingSeconds = (int)(user.LockoutUntil.Value - DateTime.UtcNow).TotalSeconds;
                throw new Exception($"Account locked. Try again after {remainingSeconds} seconds");
            }

            // 2) Verify password
            bool isMatch = _hasher.Verify(dto.Password, user.Password);

            if (!isMatch)
            {
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= 3)
                {
                    user.LockoutUntil = DateTime.UtcNow.AddMinutes(1); // Lock 1 minute

                    await _audit.LogAsync(
                         user.Id,
                         user.Role,
                         "AccountLocked",
                         null,
                      $"User {user.Email} account locked for 1 minute"
                        );

                }

                await _repo.UpdateAsync(user.Id, user);
                await _audit.LogAsync(
                     user.Id,
                      user.Role,
                  "LoginFailed",
                   null,
                 $"Wrong password for {user.Email}"
                        );
                throw new Exception("Invalid email or password");
            }

            // 3) Reset counters
            user.FailedLoginAttempts = 0;
            user.LockoutUntil = null;

            //  TWO-FACTOR AUTH
       
            if (user.TwoFactorEnabled)
            {
                string code = new Random().Next(100000, 999999).ToString();

                user.TwoFactorCode = code;
                user.TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5);

                await _repo.UpdateAsync(user.Id, user);

                await _email.SendEmailAsync(
                    user.Email,
                    "Your Verification Code",
                    $"Your login verification code is: {code}"
                );
                await _audit.LogAsync(
                  user.Id,
                  user.Role,
                  "2FACodeSent",
                   null,
                  "Two-factor verification code sent"
                  );

                return new AuthResult
                {
                    Requires2FA = true,
                    Message = "Verification code sent to your email."
                };
            }


            // لو 2FA غير مفعّل → توكن طبيعي
            string token = _token.GenerateJwtToken(user);
            await _repo.UpdateAsync(user.Id, user);

            await _audit.LogAsync(
              user.Id,
              user.Role,
              "LoginSuccess",
               null,
              $"User {user.Email} logged in successfully"
            );

            return new AuthResult
            {
                Token = token,
                User = user,
                Requires2FA = false
            };

        } 

        // LOGOUT
        public Task<string> Logout()
        {
       // JWT Stateless
            return Task.FromResult("Logged out successfully");
        }

        // UPDATE USER (Admin)
        public async Task<string> AssignRole(string id, string newRole)
        {
            var user = await _repo.GetByIdAsync(id);
            if (user == null)
                throw new Exception("User not found");

            // Validate roles
            var validRoles = new[] { "Admin", "Manager", "User" };
            if (!validRoles.Contains(newRole))
                throw new Exception("Invalid role");

            user.Role = newRole;
            user.UpdatedAt = DateTime.Now;

            await _repo.UpdateAsync(id, user);

            return "Role updated successfully";
        }


        // DELETE USER (Admin)
        public async Task<string> DeleteUser(string id, string requesterRole)
        {
            if (requesterRole != "Admin")
                return "Access denied";

            await _repo.DeleteAsync(id);
            return "User deleted successfully";
        }
        public async Task<string> UpdateProfile(string userId, UpdateProfileDto dto)
        {
            var user = await _repo.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            // تعديل الاسم والإيميل دائماً مسموح
            if (!string.IsNullOrEmpty(dto.Name))
                user.Name = dto.Name;

            if (!string.IsNullOrEmpty(dto.Email))
                user.Email = dto.Email;

            user.UpdatedAt = DateTime.Now;
            await _repo.UpdateAsync(userId, user);

            if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
            {
                var exists = await _repo.GetByEmailAsync(dto.Email);
                if (exists != null)
                    throw new Exception("Email already in use");

                user.Email = dto.Email;
            }

            await _audit.LogAsync(
              user.Id,
              user.Role,
              "UpdateProfile",
              null,
              $"User updated profile. Name: {user.Name}, Email: {user.Email}"
              );
            return "Profile updated successfully";
        }
        public async Task<string> ChangePassword(string userId, ChangePasswordDto dto)
        {
            var user = await _repo.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            if (string.IsNullOrEmpty(dto.CurrentPassword))
                throw new Exception("Current password is required");       

            if (string.IsNullOrEmpty(dto.NewPassword))
                throw new Exception("New password is required");

            bool match = _hasher.Verify(dto.CurrentPassword, user.Password);
            if (!match)
                throw new Exception("Current password is incorrect");

            user.Password = _hasher.Hash(dto.NewPassword);
            user.UpdatedAt = DateTime.Now;

            await _repo.UpdateAsync(userId, user);

            await _audit.LogAsync(
                user.Id,
                user.Role,
                "ChangePassword",
                null,
                "User changed password"
            );

            return "Password updated successfully";
        }

        // =========================
        // 2FA (Two-Factor Authentication)
        // =========================

        public async Task<bool> GetTwoFactorEnabled(string userId)
        {
            var user = await _repo.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            return user.TwoFactorEnabled;
        }

        public async Task<string> SetTwoFactorEnabled(string userId, bool enabled)
        {
            var user = await _repo.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            user.TwoFactorEnabled = enabled;

            // If disabling, clear any in-flight code
            if (!enabled)
            {
                user.TwoFactorCode = null;
                user.TwoFactorExpiry = null;
            }

            user.UpdatedAt = DateTime.Now;
            await _repo.UpdateAsync(userId, user);

            await _audit.LogAsync(
                user.Id,
                user.Role,
                "Toggle2FA",
                null,
                $"User {(enabled ? "enabled" : "disabled")} two-factor authentication"
            );

            return enabled ? "Two-factor authentication enabled" : "Two-factor authentication disabled";
        }

        public async Task<User> CreateAdmin(CreateAdminDto dto)
        {
            var exists = await _repo.GetByEmailAsync(dto.Email);
            if (exists != null)
                throw new Exception("Admin email already exists");

            string hashedPassword = _hasher.Hash(dto.Password);

            var admin = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Password = hashedPassword,
                Role = "Admin"
            };

            await _repo.CreateAsync(admin);

            return admin;
        }
        public async Task<string> EditUser(string id, UpdateUserDto dto)
        {
            var user = await _repo.GetByIdAsync(id);
            if (user == null)
                throw new Exception("User not found");

            // تعديل الاسم
            if (!string.IsNullOrEmpty(dto.Name))
                user.Name = dto.Name;

            // تعديل الإيميل
            if (!string.IsNullOrEmpty(dto.Email))
                user.Email = dto.Email;

            // تعديل كلمة المرور من قبل الأدمن
            if (!string.IsNullOrEmpty(dto.NewPassword))
                user.Password = _hasher.Hash(dto.NewPassword);

            user.UpdatedAt = DateTime.Now;

            await _repo.UpdateAsync(id, user);

            return "User updated successfully";
        }
        // توليد كود OTP من 6 أرقام
        private string GenerateResetCode()
        {
            return new Random().Next(100000, 999999).ToString();
        }
        public async Task<string> ForgotPassword(ForgotPasswordDto dto)
        {
            var user = await _repo.GetByEmailAsync(dto.Email);
            if (user == null)
                throw new Exception("Email not found");

            string code = GenerateResetCode();

            user.ResetCode = code;
            user.ResetCodeExpiry = DateTime.UtcNow.AddMinutes(5);

            await _repo.UpdateAsync(user.Id, user);

            await _email.SendEmailAsync(
                user.Email,
                "Password Reset Code",
                $"Your password reset code is: {code}"
            );

            return "Reset code sent to your email";
        }

        public async Task<string> ResetPassword(ResetPasswordDto dto)
        {
            var user = await _repo.GetByEmailAsync(dto.Email);
            if (user == null)
                throw new Exception("Invalid email");

            if (user.ResetCode != dto.Code)
                throw new Exception("Invalid reset code");

            if (user.ResetCodeExpiry < DateTime.UtcNow)
                throw new Exception("Reset code expired");

            user.Password = _hasher.Hash(dto.NewPassword);
            user.ResetCode = null;
            user.ResetCodeExpiry = null;

            await _repo.UpdateAsync(user.Id, user);

            return "Password has been reset successfully";
        }

        public async Task<List<UserDto>> GetUsers(string? role, string? search, string currentUserId)
        {
            var users = await _repo.GetAllAsync();

            // استثناء المستخدم الحالي من النتائج
            users = users.Where(u => u.Id != currentUserId).ToList();

            // فلترة حسب الدور
            if (!string.IsNullOrEmpty(role))
            {
                users = users
                        .Where(u => u.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
                        .ToList();
            }

            // بحث بالاسم أو الإيميل
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                users = users.Where(u =>
                    u.Name.ToLower().Contains(search) ||
                    u.Email.ToLower().Contains(search)
                ).ToList();
            }

            return users.Select(u => new UserDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role
            }).ToList();
        }
        public async Task CreateBootstrapAdminIfNotExists()
        {
            var adminExists = await _repo.GetByRoleAsync("Admin");
            if (adminExists.Any()) return;

            var name = _config["BootstrapAdmin:Name"];
            var email = _config["BootstrapAdmin:Email"];
            var password = _config["BootstrapAdmin:Password"];

            var hashed = _hasher.Hash(password);

            await _repo.CreateAsync(new User
            {
                Name = name,
                Email = email,
                Password = hashed,
                Role = "Admin"
            });
        }

        public async Task<AuthResult> VerifyTwoFactorAsync(Verify2FADto dto)
        {
            var user = await _repo.GetByEmailAsync(dto.Email);
            if (user == null)
                throw new Exception("User not found");

            if (user.TwoFactorCode == null || user.TwoFactorExpiry < DateTime.UtcNow)
                throw new Exception("Verification code expired");

            if (user.TwoFactorCode != dto.Code)
                throw new Exception("Invalid verification code");

            // Clear code after success
            user.TwoFactorCode = null;
            user.TwoFactorExpiry = null;

            string token = _token.GenerateJwtToken(user);

            await _repo.UpdateAsync(user.Id, user);

            await _audit.LogAsync(
                user.Id,
                user.Role,
                "Login2FASuccess",
                null,
                $"User {user.Email} passed 2FA"
            );

            return new AuthResult
            {
                Token = token,
                User = user,
                Requires2FA = false
            };
        }



    }

}
