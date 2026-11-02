using System.Security.Claims;
using System.Text.Encodings.Web;
using HelpdeskOps.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

// Runs the real app, with its routes, filters, and authorization, against SQLite. The person making a request comes
// from the X-Test-User and X-Test-Role headers, and a request without them is anonymous.
public class TestApp : WebApplicationFactory<Program>
{
    public const string UserHeader = "X-Test-User";
    public const string RoleHeader = "X-Test-Role";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));

            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>(HeaderAuthentication.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = HeaderAuthentication.SchemeName;
                options.DefaultChallengeScheme = HeaderAuthentication.SchemeName;
                options.DefaultForbidScheme = HeaderAuthentication.SchemeName;
            });
        });
    }

    public HttpClient ClientFor(string? userId = null, string? role = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (userId != null)
        {
            client.DefaultRequestHeaders.Add(UserHeader, userId);
        }
        if (role != null)
        {
            client.DefaultRequestHeaders.Add(RoleHeader, role);
        }
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }

    private class HeaderAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestHeaders";

        public HeaderAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Name, userId.ToString()) };
            if (Request.Headers.TryGetValue(RoleHeader, out var role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
