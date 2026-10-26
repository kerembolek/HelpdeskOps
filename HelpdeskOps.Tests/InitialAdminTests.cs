using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HelpdeskOps.Tests;

// Runs against a real UserManager on in-memory SQLite, the way the startup code calls it
public class InitialAdminTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services;
    private readonly UserManager<ApplicationUser> _users;

    public InitialAdminTests()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        _services = services.BuildServiceProvider();
        _services.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();

        var roles = _services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
            roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();

        _users = _services.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    private async Task<ApplicationUser> AddUserAsync(string email, string role)
    {
        var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Ad", LastName = "Soyad" };
        await _users.CreateAsync(user);
        await _users.AddToRoleAsync(user, role);
        return user;
    }

    [Fact]
    public async Task TheRegisteredUser_BecomesTheOnlyRoleITAdmin()
    {
        var user = await AddUserAsync("kerem@sirket.com", AppRoles.Employee);

        var message = await InitialAdmin.EnsureAsync(_users, " kerem@sirket.com ");

        Assert.Contains("first IT staff member", message);
        Assert.Equal(new[] { AppRoles.ITAdmin }, await _users.GetRolesAsync(user));
    }

    [Fact]
    public async Task OnceITStaffExist_TheSettingDoesNothing()
    {
        await AddUserAsync("it@sirket.com", AppRoles.ITAdmin);
        var other = await AddUserAsync("kerem@sirket.com", AppRoles.Employee);

        Assert.Null(await InitialAdmin.EnsureAsync(_users, "kerem@sirket.com"));
        Assert.Equal(new[] { AppRoles.Employee }, await _users.GetRolesAsync(other));
    }

    [Fact]
    public async Task AnUnknownEmail_IsReportedAndNothingChanges()
    {
        var message = await InitialAdmin.EnsureAsync(_users, "yok@sirket.com");

        Assert.Contains("register that account first", message);
        Assert.Empty(await _users.GetUsersInRoleAsync(AppRoles.ITAdmin));
    }

    [Fact]
    public async Task NoSetting_DoesNothing()
    {
        Assert.Null(await InitialAdmin.EnsureAsync(_users, null));
        Assert.Null(await InitialAdmin.EnsureAsync(_users, " "));
    }
}
