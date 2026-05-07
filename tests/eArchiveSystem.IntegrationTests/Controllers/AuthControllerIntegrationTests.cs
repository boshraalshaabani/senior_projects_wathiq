using eArchiveSystem.IntegrationTests.Infrastructure;

namespace eArchiveSystem.IntegrationTests.Controllers;

[Trait("Layer", "Integration")]
[Trait("Area", "Auth")]
public class AuthControllerIntegrationTests
{
    [Fact]
    public async Task Login_ReturnsJwtAndUserPayload_ForValidCredentials()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        var user = new User
        {
            Id = "employee-1",
            Name = "Integration Employee",
            Email = "employee@example.com",
            Password = factory.HashPassword("Pass123!"),
            Role = ApplicationRoles.Employee,
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "Records"
        };

        factory.State.Users[user.Id] = user;

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = user.Email,
            Password = "Pass123!"
        });

        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        Assert.False(root.GetProperty("requires2FA").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("token").GetString()));
        Assert.Equal(user.Email, root.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal(user.Role, root.GetProperty("user").GetProperty("role").GetString());
        Assert.Contains(factory.State.AuditEntries, entry => entry.Action == "LoginSuccess");
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_ForInvalidPassword()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        var user = new User
        {
            Id = "employee-2",
            Name = "Integration Employee",
            Email = "employee2@example.com",
            Password = factory.HashPassword("Pass123!"),
            Role = ApplicationRoles.Employee,
            InstitutionId = "inst-a",
            DepartmentId = "dept-a",
            Department = "Records"
        };

        factory.State.Users[user.Id] = user;

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = user.Email,
            Password = "WrongPass!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        Assert.Equal("Invalid email or password", root.GetProperty("message").GetString());
        Assert.Contains(factory.State.AuditEntries, entry => entry.Action == "LoginFailed");
    }
}
