using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Data;

public static class DemoDataSeeder
{
    // Local development only. These accounts are never created outside Development.
    private const string DemoPassword = "Helpdesk2026!";

    private static readonly (string Email, string FirstName, string LastName, string Department, string Role)[] DemoUsers =
    {
        ("test@helpdeskops.com", "Onur", "Güçlü", "Muhasebe", "Employee"),
        ("it@helpdeskops.com", "Bilgehan", "Sungur", "Bilgi Teknolojileri", "ITAdmin"),
    };

    // Matched by serial number, so devices added or edited by hand are left alone
    private static readonly (string Hostname, string Ip, string Serial, string Brand, string Model, DeviceType Type, int WarrantyYears, string? AssignedEmail)[] DemoDevices =
    {
        ("MUH-LT-001", "10.10.20.11", "DEMO-LT-001", "Lenovo", "ThinkPad E14", DeviceType.Laptop, 2, "test@helpdeskops.com"),
        ("MUH-MN-001", "10.10.20.12", "DEMO-MN-001", "Dell", "P2422H", DeviceType.Monitor, 3, "test@helpdeskops.com"),
        ("BT-DT-001", "10.10.10.21", "DEMO-DT-001", "HP", "EliteDesk 800 G6", DeviceType.Desktop, 1, "it@helpdeskops.com"),
        ("MUH-PR-001", "10.10.20.50", "DEMO-PR-001", "HP", "LaserJet Pro M404dn", DeviceType.Printer, 1, null),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var demo in DemoUsers)
        {
            var user = await userManager.FindByEmailAsync(demo.Email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = demo.Email,
                    Email = demo.Email,
                    EmailConfirmed = true,
                    FirstName = demo.FirstName,
                    LastName = demo.LastName,
                    Department = demo.Department
                };

                var result = await userManager.CreateAsync(user, DemoPassword);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Could not seed {demo.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            if (!await userManager.IsInRoleAsync(user, demo.Role))
                await userManager.AddToRoleAsync(user, demo.Role);
        }

        await SeedDevicesAsync(services, userManager);
    }

    private static async Task SeedDevicesAsync(IServiceProvider services, UserManager<ApplicationUser> userManager)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        foreach (var demo in DemoDevices)
        {
            if (await context.Devices.AnyAsync(d => d.SerialNumber == demo.Serial))
                continue;

            var assignedUser = demo.AssignedEmail != null ? await userManager.FindByEmailAsync(demo.AssignedEmail) : null;

            context.Devices.Add(new Device
            {
                Hostname = demo.Hostname,
                IpAddress = demo.Ip,
                SerialNumber = demo.Serial,
                Brand = demo.Brand,
                Model = demo.Model,
                DeviceType = demo.Type,
                Status = assignedUser != null ? DeviceStatus.Active : DeviceStatus.InStorage,
                WarrantyEndDate = DateTime.Today.AddYears(demo.WarrantyYears),
                AssignedUserId = assignedUser?.Id,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }
}