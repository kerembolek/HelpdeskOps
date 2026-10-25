using System.Net;
using System.Security.Claims;
using System.Text.Json;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Tests;

// Runs the interceptor against a real (in-memory SQLite) database, the way it runs inside a web request
public class AuditInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly HttpContextAccessor _http = new();
    private readonly ApplicationDbContext _context;

    public AuditInterceptorTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditInterceptor(_http))
            .Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _context.Users.Add(new ApplicationUser { Id = "it-1", UserName = "it@x.com", Email = "it@x.com", FirstName = "Bilgehan", LastName = "Sungur" });
        _context.Users.Add(new ApplicationUser { Id = "emp-1", UserName = "test@x.com", Email = "test@x.com", FirstName = "Onur", LastName = "Güçlü" });
        _context.Roles.Add(new IdentityRole("ITAdmin") { Id = "role-it", NormalizedName = "ITADMIN" });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void SignIn()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "it-1"),
                new Claim(ClaimTypes.Name, "it@x.com")
            }, "test"))
        };
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.7");
        _http.HttpContext = context;
    }

    private List<AuditChange> ChangesOf(AuditLog log) => JsonSerializer.Deserialize<List<AuditChange>>(log.Changes!)!;

    private static Device NewDevice() => new()
    {
        Hostname = "PC-01",
        IpAddress = "10.0.0.5",
        SerialNumber = "SN-1",
        DeviceType = DeviceType.Laptop,
        Status = DeviceStatus.InStorage
    };

    [Fact]
    public async Task ChangesOutsideARequest_AreNotRecorded()
    {
        _context.Devices.Add(NewDevice());
        await _context.SaveChangesAsync();

        Assert.Empty(_context.AuditLogs);
    }

    [Fact]
    public async Task CreatingADevice_RecordsWhoWhenWhereAndTheValues()
    {
        SignIn();
        _context.Devices.Add(NewDevice());
        await _context.SaveChangesAsync();

        var log = Assert.Single(_context.AuditLogs);
        Assert.Equal(AuditAction.Created, log.Action);
        Assert.Equal("Device", log.EntityType);
        Assert.Equal("PC-01", log.EntityLabel);
        Assert.Equal("it-1", log.UserId);
        Assert.Equal("it@x.com", log.UserName);
        Assert.Equal("10.0.0.7", log.IpAddress);
        Assert.NotNull(log.EntityId);
        Assert.Contains(ChangesOf(log), c => c.Field == "Cihaz Türü" && c.New == "Dizüstü Bilgisayar");
    }

    [Fact]
    public async Task UpdatingADevice_RecordsOnlyChangedFieldsWithNames()
    {
        var device = NewDevice();
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        SignIn();
        device.Status = DeviceStatus.Active;
        device.AssignedUserId = "emp-1";
        device.LastUpdated = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var log = Assert.Single(_context.AuditLogs);
        var changes = ChangesOf(log);
        Assert.Equal(AuditAction.Updated, log.Action);
        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c.Field == "Cihaz Durumu" && c.Old == "Depoda" && c.New == "Aktif");
        Assert.Contains(changes, c => c.Field == "Zimmetli Kullanıcı" && c.Old == null && c.New == "Onur Güçlü");
    }

    [Fact]
    public async Task HousekeepingOnlyUpdates_AreSkipped()
    {
        var device = NewDevice();
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        SignIn();
        device.LastUpdated = DateTime.UtcNow.AddMinutes(5);
        await _context.SaveChangesAsync();

        Assert.Empty(_context.AuditLogs);
    }

    [Fact]
    public async Task DeletingADevice_KeepsItsLabel()
    {
        var device = NewDevice();
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        SignIn();
        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();

        var log = Assert.Single(_context.AuditLogs);
        Assert.Equal(AuditAction.Deleted, log.Action);
        Assert.Equal("PC-01", log.EntityLabel);
    }

    [Fact]
    public async Task PasswordChanges_NeverStoreTheHash()
    {
        SignIn();
        var user = await _context.Users.SingleAsync(u => u.Id == "emp-1");
        user.PasswordHash = "AQAAAAIAAYagAAAAEsecret";
        await _context.SaveChangesAsync();

        var log = Assert.Single(_context.AuditLogs);
        Assert.DoesNotContain("secret", log.Changes);
        Assert.Contains(ChangesOf(log), c => c.Field == "Şifre" && c.New == "(değiştirildi)");
    }

    [Fact]
    public async Task RoleChanges_NameTheRoleAndThePerson()
    {
        SignIn();
        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = "emp-1", RoleId = "role-it" });
        await _context.SaveChangesAsync();

        var log = Assert.Single(_context.AuditLogs);
        Assert.Equal("UserRole", log.EntityType);
        Assert.Equal("Onur Güçlü", log.EntityLabel);
        Assert.Contains(ChangesOf(log), c => c.Field == "Rol" && c.New == AppRoles.DisplayName("ITAdmin"));
    }

    [Fact]
    public async Task UnauditedTypes_AreIgnored()
    {
        SignIn();
        _context.Notifications.Add(new Notification { UserId = "emp-1", Message = "x", Url = "/" });
        await _context.SaveChangesAsync();

        Assert.Empty(_context.AuditLogs);
    }
}
