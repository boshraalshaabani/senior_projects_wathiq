using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Security;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Domain.Models;
using eArchiveSystem.Infrastructure.Security;
using eArchiveSystem.TestHost.TestDoubles;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Text;

namespace eArchiveSystem.TestHost.Infrastructure;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string JwtKey = "Wathiq_Local_Development_Key_Change_Me_2026";
    private const string JwtIssuer = "eArchiveSystem";
    private const string JwtAudience = "eArchiveSystemUsers";

    public IntegrationTestState State { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(ResolveApplicationContentRoot());

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = JwtKey,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["BootstrapAdmin:Name"] = "",
                ["BootstrapAdmin:Email"] = "",
                ["BootstrapAdmin:Password"] = ""
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IDocumentRepository>();
            services.RemoveAll<IMetadataRepository>();
            services.RemoveAll<IDepartmentRepository>();
            services.RemoveAll<IAuditService>();
            services.RemoveAll<IEmailService>();
            services.RemoveAll<IIndexingService>();
            services.RemoveAll<INotificationService>();
            services.RemoveAll<IPasswordHasher>();
            services.RemoveAll<ITokenService>();

            services.AddSingleton(State);

            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
            services.AddSingleton<IMetadataRepository, InMemoryMetadataRepository>();
            services.AddSingleton<IDepartmentRepository, InMemoryDepartmentRepository>();
            services.AddSingleton<IAuditService, TrackingAuditService>();
            services.AddSingleton<IEmailService, RecordingEmailService>();
            services.AddSingleton<IIndexingService, TrackingIndexingService>();
            services.AddSingleton<INotificationService, TrackingNotificationService>();
            services.AddSingleton<IPasswordHasher, TestPasswordHasher>();
            services.AddScoped<ITokenService, JwtTokenService>();
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = JwtIssuer,
                    ValidAudience = JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey))
                };
            });
        });
    }

    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    public string HashPassword(string password)
    {
        using var scope = Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        return hasher.Hash(password);
    }

    public string CreateToken(User user)
    {
        using var scope = Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerateJwtToken(user);
    }

    public HttpClient CreateAuthenticatedClient(User user)
    {
        var client = CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(user));
        return client;
    }

    private static string ResolveApplicationContentRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var applicationRoot = Path.Combine(current.FullName, "eArchiveSystem");
            var projectFile = Path.Combine(applicationRoot, "eArchiveSystem.csproj");

            if (File.Exists(projectFile))
            {
                return applicationRoot;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the eArchiveSystem content root from base directory '{AppContext.BaseDirectory}'.");
    }
}
