using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Services.Inventory;

// Applies an agent report to the device with the same serial number, or adds the computer as a new device
public class InventoryImporter
{
    private readonly ApplicationDbContext _context;

    public InventoryImporter(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(Device Device, bool Created)> ApplyAsync(InventoryReport report)
    {
        var serial = report.SerialNumber.Trim();
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == serial);
        var created = device == null;
        if (device == null)
        {
            device = new Device
            {
                Hostname = report.Hostname.Trim(),
                IpAddress = report.IpAddress,
                SerialNumber = serial,
                DeviceType = report.DeviceType,
                Status = DeviceStatus.Active
            };
            _context.Devices.Add(device);
        }

        // The computer knows its current name and address better than the inventory does (renames, DHCP)
        device.Hostname = report.Hostname.Trim();
        device.IpAddress = report.IpAddress;

        // Brand and model stay as IT typed them; the agent only fills them in when they are empty
        device.Brand ??= Clean(report.Manufacturer);
        device.Model ??= Clean(report.Model);

        device.Processor = Clean(report.Processor);
        device.RamGb = report.RamGb;
        device.DiskGb = report.DiskGb;
        device.OsVersion = Clean(report.OsVersion);
        device.LastInventoryAt = DateTime.UtcNow;
        device.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return (device, created);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
