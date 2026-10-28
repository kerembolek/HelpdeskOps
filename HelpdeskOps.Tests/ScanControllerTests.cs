using System.Security.Claims;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Tests;

// Where a scanned device sticker (/d/{id}) sends each role
public class ScanControllerTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ApplicationDbContext _context;
    private readonly int _deviceId;

    public ScanControllerTests()
    {
        _connection.Open();
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        var device = new Device
        {
            Hostname = "MUH-PR-001", IpAddress = "10.0.0.9", SerialNumber = "PR-1",
            DeviceType = DeviceType.Printer, Status = DeviceStatus.Active
        };
        _context.Devices.Add(device);
        _context.SaveChanges();
        _deviceId = device.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private ScanController ControllerFor(string? role)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1") };
        if (role != null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ScanController(_context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }

    [Fact]
    public async Task ITStaff_GoToTheDeviceCard()
    {
        var result = Assert.IsType<RedirectToActionResult>(await ControllerFor("ITAdmin").Device(_deviceId));

        Assert.Equal(("Devices", "Details"), (result.ControllerName, result.ActionName));
        Assert.Equal(_deviceId, result.RouteValues!["id"]);
    }

    [Theory]
    [InlineData("Employee")]
    [InlineData("Manager")]
    public async Task OtherRoles_GoToANewTicketForTheDevice(string role)
    {
        var result = Assert.IsType<RedirectToActionResult>(await ControllerFor(role).Device(_deviceId));

        Assert.Equal(("Tickets", "Create"), (result.ControllerName, result.ActionName));
        Assert.Equal(_deviceId, result.RouteValues!["deviceId"]);
    }

    [Fact]
    public async Task UnknownDevice_IsNotFound()
    {
        Assert.IsType<NotFoundResult>(await ControllerFor("Employee").Device(99999));
    }
}
