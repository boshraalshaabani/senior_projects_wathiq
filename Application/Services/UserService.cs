using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Exceptions;
using eArchiveSystem.Application.Interfaces.Security;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Application.Security;
using eArchiveSystem.Domain.Models;
using eArchiveSystem.Utils;

namespace eArchiveSystem.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly IDepartmentRepository _departments;
        private readonly IPasswordHasher _hasher;
        private readonly ITokenService _token;
        private readonly IEmailService _email;
        private readonly IConfiguration _config;
        private readonly IAuditService _audit;



        public UserService(
      IUserRepository repo,
      IDepartmentRepository departments,
      IPasswordHasher hasher,
      ITokenService token,
      IEmailService email,
      IConfiguration config,
      IAuditService audit
  )
        {
            _repo = repo;
            _departments = departments;
            _hasher = hasher;
            _token = token;
            _email = email;
            _config = config;
            _audit = audit;
        }


        // ADD USER  (Instead of Register)
        public async Task<User> AddUser(AddUserDto dto, string requesterId)
        {
            var requester = await _repo.GetByIdAsync(requesterId);
            if (requester == null)
                throw new NotFoundException("Requester not found");

            var exists = await _repo.GetByEmailAsync(dto.Email);
            if (exists != null)
                throw new ConflictException("Email already used");

            var validRoles = new[]
            {
                ApplicationRoles.SystemAdmin,
                ApplicationRoles.InstitutionAdmin,
                ApplicationRoles.Manager,
                ApplicationRoles.Employee
            };

            if (!validRoles.Contains(dto.Role))
                throw new ValidationException("Invalid role");

            if (ApplicationRoles.IsInstitutionAdmin(requester.Role))
            {
                if (dto.Role != ApplicationRoles.Manager &&
                    dto.Role != ApplicationRoles.Employee)
                {
                    throw new UnauthorizedActionException("Institution admin can only create managers and employees");
                }

                dto.InstitutionId = requester.InstitutionId;
            }
            else if (!ApplicationRoles.IsSystemAdmin(requester.Role))
            {
                throw new UnauthorizedActionException("You are not allowed to create users");
            }

            await ApplyDepartmentAssignmentAsync(dto.InstitutionId, dto.DepartmentId ?? dto.Department, user: null, dto);

            string hashedPassword = _hasher.Hash(dto.Password);

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Password = hashedPassword,
                Role = dto.Role,
                InstitutionId = dto.InstitutionId,
                DepartmentId = dto.DepartmentId ?? dto.Department,
                Department = dto.Department ?? dto.DepartmentId
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

                throw new ValidationException("Invalid email or password");
            }


            // 1) Check if user is locked
            if (user.LockoutUntil != null && user.LockoutUntil > DateTime.UtcNow)
            {
                var remainingSeconds = (int)(user.LockoutUntil.Value - DateTime.UtcNow).TotalSeconds;
                throw new ValidationException($"Account locked. Try again after {remainingSeconds} seconds");
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
                throw new ValidationException("Invalid email or password");
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
        public async Task<string> AssignRole(string id, string newRole, string requesterId)
        {
            var requester = await _repo.GetByIdAsync(requesterId);
            if (requester == null)
                throw new NotFoundException("Requester not found");

            var user = await _repo.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException("User not found");

            // Validate roles
            var validRoles = new[] { ApplicationRoles.SystemAdmin, ApplicationRoles.InstitutionAdmin, ApplicationRoles.Manager, ApplicationRoles.Employee };
            if (!validRoles.Contains(newRole))
                throw new ValidationException("Invalid role");

            if (ApplicationRoles.IsInstitutionAdmin(requester.Role))
            {
                if (!string.Equals(requester.InstitutionId, user.InstitutionId, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedActionException("You can only manage users in your institution");

                if (newRole != ApplicationRoles.Manager &&
                    newRole != ApplicationRoles.Employee)
                {
                    throw new UnauthorizedActionException("Institution admin can only assign manager or employee roles");
                }
            }
            else if (!ApplicationRoles.IsSystemAdmin(requester.Role))
            {
                throw new UnauthorizedActionException("You are not allowed to assign roles");
            }

            user.Role = newRole;
            user.UpdatedAt = DateTime.Now;

            await _repo.UpdateAsync(id, user);

            return "Role updated successfully";
        }


        // DELETE USER (Admin)
        public async Task<string> DeleteUser(string id, string requesterRole, string requesterId)
        {
            var requester = await _repo.GetByIdAsync(requesterId);
            if (requester == null)
                throw new NotFoundException("Requester not found");

            var target = await _repo.GetByIdAsync(id);
            if (target == null)
                throw new NotFoundException("User not found");

            if (ApplicationRoles.IsInstitutionAdmin(requester.Role))
            {
                if (!string.Equals(requester.InstitutionId, target.InstitutionId, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedActionException("You can only delete users in your institution");

                if (ApplicationRoles.IsSystemAdmin(target.Role) || ApplicationRoles.IsInstitutionAdmin(target.Role))
                    throw new UnauthorizedActionException("Institution admin cannot delete admin accounts");
            }
            else if (!ApplicationRoles.IsSystemAdmin(requesterRole))
            {
                throw new UnauthorizedActionException("Access denied");
            }

            await _repo.DeleteAsync(id);
            return "User deleted successfully";
        }
        public async Task<string> UpdateProfile(string userId, UpdateProfileDto dto)
        {
            var user = await _repo.GetByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            // تعديل الاسم والإيميل دائماً مسموح
            if (!string.IsNullOrEmpty(dto.Name))
                user.Name = dto.Name;

            if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
            {
                var exists = await _repo.GetByEmailAsync(dto.Email);
                if (exists != null)
                    throw new ConflictException("Email already in use");

                user.Email = dto.Email;
            }

            user.UpdatedAt = DateTime.Now;
            await _repo.UpdateAsync(userId, user);

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
                throw new NotFoundException("User not found");

            if (string.IsNullOrEmpty(dto.CurrentPassword))
                throw new ValidationException("Current password is required");       

            if (string.IsNullOrEmpty(dto.NewPassword))
                throw new ValidationException("New password is required");

            bool match = _hasher.Verify(dto.CurrentPassword, user.Password);
            if (!match)
                throw new ValidationException("Current password is incorrect");

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
                throw new NotFoundException("User not found");

            return user.TwoFactorEnabled;
        }

        public async Task<string> SetTwoFactorEnabled(string userId, bool enabled)
        {
            var user = await _repo.GetByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

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
                throw new ConflictException("System admin email already exists");

            string hashedPassword = _hasher.Hash(dto.Password);

            var admin = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Password = hashedPassword,
                Role = ApplicationRoles.SystemAdmin
            };

            await _repo.CreateAsync(admin);

            return admin;
        }
        public async Task<string> EditUser(string id, UpdateUserDto dto, string requesterId)
        {
            var requester = await _repo.GetByIdAsync(requesterId);
            if (requester == null)
                throw new NotFoundException("Requester not found");

            var user = await _repo.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException("User not found");

            if (ApplicationRoles.IsInstitutionAdmin(requester.Role))
            {
                if (!string.Equals(requester.InstitutionId, user.InstitutionId, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedActionException("You can only edit users in your institution");

                if (ApplicationRoles.IsSystemAdmin(user.Role) || ApplicationRoles.IsInstitutionAdmin(user.Role))
                    throw new UnauthorizedActionException("Institution admin cannot edit admin accounts");
            }
            else if (!ApplicationRoles.IsSystemAdmin(requester.Role))
            {
                throw new UnauthorizedActionException("You are not allowed to edit users");
            }

            // تعديل الاسم
            if (!string.IsNullOrEmpty(dto.Name))
                user.Name = dto.Name;

            // تعديل الإيميل
            if (!string.IsNullOrEmpty(dto.Email))
                user.Email = dto.Email;

            // تعديل كلمة المرور من قبل الأدمن
            if (!string.IsNullOrEmpty(dto.NewPassword))
                user.Password = _hasher.Hash(dto.NewPassword);

            if (!string.IsNullOrWhiteSpace(dto.InstitutionId) && ApplicationRoles.IsSystemAdmin(requester.Role))
                user.InstitutionId = dto.InstitutionId;

            if (!string.IsNullOrWhiteSpace(dto.DepartmentId) || !string.IsNullOrWhiteSpace(dto.Department))
            {
                await ApplyDepartmentAssignmentAsync(user.InstitutionId, dto.DepartmentId ?? dto.Department, user, dto);
            }

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
                throw new NotFoundException("Email not found");

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
                throw new ValidationException("Invalid email");

            if (user.ResetCode != dto.Code)
                throw new ValidationException("Invalid reset code");

            if (user.ResetCodeExpiry < DateTime.UtcNow)
                throw new ValidationException("Reset code expired");

            user.Password = _hasher.Hash(dto.NewPassword);
            user.ResetCode = null;
            user.ResetCodeExpiry = null;

            await _repo.UpdateAsync(user.Id, user);

            return "Password has been reset successfully";
        }

        public async Task<List<UserDto>> GetUsers(string? role, string? search, string currentUserId)
        {
            var users = await _repo.GetAllAsync();
            var currentUser = await _repo.GetByIdAsync(currentUserId);

            // استثناء المستخدم الحالي من النتائج
            users = users.Where(u => u.Id != currentUserId).ToList();

            // فلترة حسب الدور
            if (!string.IsNullOrEmpty(role))
            {
                users = users
                        .Where(u => u.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
                        .ToList();
            }

            if (currentUser != null && ApplicationRoles.IsInstitutionAdmin(currentUser.Role))
            {
                users = users
                    .Where(u => string.Equals(u.InstitutionId, currentUser.InstitutionId, StringComparison.OrdinalIgnoreCase))
                    .Where(u => !ApplicationRoles.IsSystemAdmin(u.Role) && !ApplicationRoles.IsInstitutionAdmin(u.Role))
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
                Role = u.Role,
                InstitutionId = u.InstitutionId,
                DepartmentId = u.DepartmentId,
                Department = u.Department
            }).ToList();
        }
        public async Task CreateBootstrapAdminIfNotExists()
        {
            var systemAdminExists = await _repo.GetByRoleAsync(ApplicationRoles.SystemAdmin);
            if (systemAdminExists.Any()) return;

            var legacyAdminExists = await _repo.GetByRoleAsync(ApplicationRoles.LegacySystemAdmin);
            if (legacyAdminExists.Any()) return;

            var name = _config["BootstrapAdmin:Name"];
            var email = _config["BootstrapAdmin:Email"];
            var password = _config["BootstrapAdmin:Password"];

            var hashed = _hasher.Hash(password);

            await _repo.CreateAsync(new User
            {
                Name = name,
                Email = email,
                Password = hashed,
                Role = ApplicationRoles.SystemAdmin
            });
        }

        public async Task<AuthResult> VerifyTwoFactorAsync(Verify2FADto dto)
        {
            var user = await _repo.GetByEmailAsync(dto.Email);
            if (user == null)
                throw new NotFoundException("User not found");

            if (user.TwoFactorCode == null || user.TwoFactorExpiry < DateTime.UtcNow)
                throw new ValidationException("Verification code expired");

            if (user.TwoFactorCode != dto.Code)
                throw new ValidationException("Invalid verification code");

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

        private async Task ApplyDepartmentAssignmentAsync(string? institutionId, string? requestedDepartmentId, User? user, AddUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(requestedDepartmentId))
            {
                dto.DepartmentId = null;
                dto.Department = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(institutionId))
                throw new ValidationException("InstitutionId is required when assigning a department");

            var department = await _departments.GetByIdAsync(requestedDepartmentId.Trim());
            if (department == null)
                throw new NotFoundException("Department not found");

            if (!string.Equals(department.InstitutionId, institutionId, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Department does not belong to the selected institution");

            dto.DepartmentId = department.Id;
            dto.Department = department.Name;

            if (user != null)
            {
                user.DepartmentId = department.Id;
                user.Department = department.Name;
            }
        }

        private async Task ApplyDepartmentAssignmentAsync(string? institutionId, string? requestedDepartmentId, User user, UpdateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(requestedDepartmentId))
            {
                user.DepartmentId = null;
                user.Department = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(institutionId))
                throw new ValidationException("InstitutionId is required when assigning a department");

            var department = await _departments.GetByIdAsync(requestedDepartmentId.Trim());
            if (department == null)
                throw new NotFoundException("Department not found");

            if (!string.Equals(department.InstitutionId, institutionId, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Department does not belong to the selected institution");

            dto.DepartmentId = department.Id;
            dto.Department = department.Name;
            user.DepartmentId = department.Id;
            user.Department = department.Name;
        }



    }

}
