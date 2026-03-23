namespace eArchiveSystem.Application.DTOs
{
    public class AddUserDto
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; } // Admin / Manager / User
        public string Department { get; set; }

    }
}
