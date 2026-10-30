using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Services.CompanyDirectory;
using HelpdeskOps.Services.Kvkk;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

public class DirectoryAccountTests : KvkkTestDatabase
{
    private sealed class FakeDirectory : IDirectoryClient, IDirectoryPasswordChecker
    {
        public List<DirectoryUser> People { get; } = new();
        public string? RightPassword { get; init; }

        public Task<IReadOnlyList<DirectoryUser>> GetUsersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DirectoryUser>>(People);

        public Task<bool> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken) =>
            Task.FromResult(password == RightPassword);
    }

    private DirectoryUserManager Manager(DirectoryProvider provider, FakeDirectory? directory = null) => new(
        new UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>(Context), Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(),
        new[] { new UserValidator<ApplicationUser>() }, Array.Empty<IPasswordValidator<ApplicationUser>>(),
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance,
        Options.Create(new DirectoryOptions { Provider = provider }), directory ?? new FakeDirectory());

    private async Task<ApplicationUser> LinkedUser()
    {
        var user = await Context.Users.FindAsync("emp-1");
        user!.DirectoryId = "d1";
        await Context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task ActiveDirectoryAccountIsCheckedByTheDirectory()
    {
        var manager = Manager(DirectoryProvider.ActiveDirectory, new FakeDirectory { RightPassword = "dizin" });
        var user = await LinkedUser();

        Assert.True(await manager.CheckPasswordAsync(user, "dizin"));
        Assert.False(await manager.CheckPasswordAsync(user, "baska"));
    }

    [Fact]
    public async Task EntraAccountNeverPassesThePasswordForm()
    {
        var manager = Manager(DirectoryProvider.Entra, new FakeDirectory { RightPassword = "dizin" });
        var user = await LinkedUser();

        Assert.False(await manager.CheckPasswordAsync(user, "dizin"));
    }

    [Fact]
    public async Task LocalAccountKeepsItsLocalPassword()
    {
        var manager = Manager(DirectoryProvider.Entra);
        var user = (await Context.Users.FindAsync("emp-1"))!;
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "yerel-sifre");

        Assert.True(await manager.CheckPasswordAsync(user, "yerel-sifre"));
    }

    [Fact]
    public async Task TurningTheDirectoryOffBringsLocalPasswordsBack()
    {
        var user = await LinkedUser();
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "yerel-sifre");

        Assert.True(await Manager(DirectoryProvider.None).CheckPasswordAsync(user, "yerel-sifre"));
    }

    [Fact]
    public async Task LinkedAccountCannotGetALocalPassword()
    {
        var manager = Manager(DirectoryProvider.Entra);
        var user = await LinkedUser();
        user.PasswordHash = null;

        var added = await manager.AddPasswordAsync(user, "yeni-sifre-123");
        var reset = await manager.ResetPasswordAsync(user, "token", "yeni-sifre-123");

        Assert.False(added.Succeeded);
        Assert.False(reset.Succeeded);
        Assert.Null(user.PasswordHash);
    }

    private DirectorySync Sync(FakeDirectory directory) => new(Context, Manager(DirectoryProvider.Entra, directory), directory);

    [Fact]
    public async Task SyncCreatesAnEmployeeWithoutAPassword()
    {
        var directory = new FakeDirectory();
        directory.People.Add(new DirectoryUser("d9", "yeni@x.com", "Yeni", "Kişi", "İK", true));

        var result = await Sync(directory).RunAsync();

        Assert.Equal(1, result.Created);
        var user = await Context.Users.SingleAsync(u => u.DirectoryId == "d9");
        Assert.Null(user.PasswordHash);
        Assert.True(user.EmailConfirmed);
        Assert.Equal("yeni@x.com", user.UserName);
        Assert.True(await Context.UserRoles.AnyAsync(r => r.UserId == user.Id && r.RoleId == "role-emp"));
        Assert.True(await Context.AuditLogs.AnyAsync(a => a.EntityType == DirectorySync.AuditEntityType));
    }

    [Fact]
    public async Task SyncLinksTheExistingAccountAndCopiesItsDetails()
    {
        var directory = new FakeDirectory();
        directory.People.Add(new DirectoryUser("d1", "onur@x.com", "Onur", "Güçlü", "Finans", true));
        directory.People.Add(new DirectoryUser("d2", "it@x.com", "Bilgehan", "Sungur", null, true));

        var result = await Sync(directory).RunAsync();

        Assert.Equal(2, result.Updated);
        var user = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == "emp-1");
        Assert.Equal("d1", user.DirectoryId);
        Assert.Equal("Finans", user.Department);
    }

    [Fact]
    public async Task DisabledPersonLosesAccessAndAllOpenSignIns()
    {
        var user = await LinkedUser();
        var other = await Context.Users.FindAsync("it-1");
        other!.DirectoryId = "d2";
        await Context.SaveChangesAsync();
        var stamp = user.SecurityStamp;

        var directory = new FakeDirectory();
        directory.People.Add(new DirectoryUser("d1", "onur@x.com", "Onur", "Güçlü", "Muhasebe", false));
        directory.People.Add(new DirectoryUser("d2", "it@x.com", "Bilgehan", "Sungur", null, true));
        directory.People.Add(new DirectoryUser("d3", "ucuncu@x.com", "Üçüncü", "Kişi", null, true));

        var result = await Sync(directory).RunAsync();

        Assert.Equal(1, result.Closed);
        var closed = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == "emp-1");
        Assert.Equal(DateTimeOffset.MaxValue, closed.LockoutEnd);
        Assert.NotEqual(stamp, closed.SecurityStamp);
    }

    [Fact]
    public async Task AnonymizingRemovesTheDirectoryLink()
    {
        var user = await LinkedUser();

        await new PersonalDataAnonymizer(Context).AnonymizeAsync(user);

        Assert.Null((await Context.Users.AsNoTracking().SingleAsync(u => u.Id == "emp-1")).DirectoryId);
    }
}
