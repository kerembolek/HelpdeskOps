using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services.Inventory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Tests;

public class InventoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ApplicationDbContext _context;
    private readonly InventoryImporter _importer;

    public InventoryTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        _importer = new InventoryImporter(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static InventoryReport Report(string serial = "SN-100") => new()
    {
        Hostname = "MUH-LT-009",
        SerialNumber = serial,
        IpAddress = "10.0.0.25",
        Manufacturer = "Dell Inc.",
        Model = "Latitude 5440",
        DeviceType = DeviceType.Laptop,
        Processor = "Intel Core i5-1335U",
        RamGb = 16,
        DiskGb = 512,
        OsVersion = "Windows 11 Pro 24H2"
    };

    [Fact]
    public async Task ApplyAsync_AddsAnUnknownComputerAsANewDevice()
    {
        var (device, created) = await _importer.ApplyAsync(Report());

        Assert.True(created);
        Assert.Equal(DeviceStatus.Active, device.Status);
        Assert.Equal(DeviceType.Laptop, device.DeviceType);
        Assert.Equal("Dell Inc.", device.Brand);
        Assert.Equal(16, device.RamGb);
        Assert.NotNull(device.LastInventoryAt);
    }

    [Fact]
    public async Task ApplyAsync_UpdatesTheDeviceWithTheSameSerialNumber()
    {
        _context.Devices.Add(new Device
        {
            Hostname = "ESKI-AD", IpAddress = "10.0.0.5", SerialNumber = "SN-100",
            Brand = "Dell", DeviceType = DeviceType.Laptop, Status = DeviceStatus.Active
        });
        await _context.SaveChangesAsync();

        var (device, created) = await _importer.ApplyAsync(Report());

        Assert.False(created);
        Assert.Single(_context.Devices);
        // Name and address follow the computer, the brand IT typed is kept, the empty model is filled in
        Assert.Equal("MUH-LT-009", device.Hostname);
        Assert.Equal("10.0.0.25", device.IpAddress);
        Assert.Equal("Dell", device.Brand);
        Assert.Equal("Latitude 5440", device.Model);
        Assert.Equal(512, device.DiskGb);
    }

    [Fact]
    public async Task ApplyAsync_TrimsTheSerialNumberBeforeMatching()
    {
        await _importer.ApplyAsync(Report());

        var (_, created) = await _importer.ApplyAsync(Report("  SN-100 "));

        Assert.False(created);
    }

    [Theory]
    [InlineData("gizli-anahtar", true)]
    [InlineData("gizli-anahtaR", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidKey_AcceptsOnlyTheExactKey(string? sent, bool expected)
    {
        Assert.Equal(expected, InventoryKeyAttribute.IsValidKey(sent, "gizli-anahtar"));
    }
}
