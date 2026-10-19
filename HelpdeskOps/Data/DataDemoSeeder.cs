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
        // Extra employees so the dashboard has more than one department to compare
        ("ik@helpdeskops.com", "Ayşe", "Demir", "İnsan Kaynakları", "Employee"),
        ("satis@helpdeskops.com", "Emre", "Kaya", "Satış", "Employee"),
    };

    // Matched by serial number, so devices added or edited by hand are left alone
    private static readonly (string Hostname, string Ip, string Serial, string Brand, string Model, DeviceType Type, int WarrantyYears, string? AssignedEmail)[] DemoDevices =
    {
        ("MUH-LT-001", "10.10.20.11", "DEMO-LT-001", "Lenovo", "ThinkPad E14", DeviceType.Laptop, 2, "test@helpdeskops.com"),
        ("MUH-MN-001", "10.10.20.12", "DEMO-MN-001", "Dell", "P2422H", DeviceType.Monitor, 3, "test@helpdeskops.com"),
        ("BT-DT-001", "10.10.10.21", "DEMO-DT-001", "HP", "EliteDesk 800 G6", DeviceType.Desktop, 1, "it@helpdeskops.com"),
        ("MUH-PR-001", "10.10.20.50", "DEMO-PR-001", "HP", "LaserJet Pro M404dn", DeviceType.Printer, 1, null),
        ("IK-LT-001", "10.10.30.11", "DEMO-LT-002", "Lenovo", "ThinkPad E14", DeviceType.Laptop, 2, "ik@helpdeskops.com"),
        ("SAT-LT-001", "10.10.40.11", "DEMO-LT-003", "Lenovo", "ThinkPad E14", DeviceType.Laptop, 2, "satis@helpdeskops.com"),
    };

    // Matched by title and requester. CreatedDaysAgo/ResolvedAfterHours place them in the past
    // so the dashboard has resolution times to average; a null ResolvedAfterHours means still active.
    private static readonly (string Title, string Description, TicketCategory Category, TicketPriority Priority, TicketStatus Status,
        string RequesterEmail, string? AssigneeEmail, string? DeviceSerial, int CreatedDaysAgo, int? ResolvedAfterHours)[] DemoTickets =
    {
        ("Dizüstü bilgisayar açılmıyor", "Güç tuşuna basınca ışık yanıyor ama ekran gelmiyor.",
            TicketCategory.Hardware, TicketPriority.High, TicketStatus.Closed,
            "test@helpdeskops.com", "it@helpdeskops.com", "DEMO-LT-001", 30, 6),
        ("Laptop fanı çok ses yapıyor", "Toplantılarda fan sesi çok yüksek, cihaz da ısınıyor.",
            TicketCategory.Hardware, TicketPriority.Medium, TicketStatus.Resolved,
            "ik@helpdeskops.com", "it@helpdeskops.com", "DEMO-LT-002", 21, 30),
        ("Batarya çabuk bitiyor", "Tam şarjla bir saat bile dayanmıyor.",
            TicketCategory.Hardware, TicketPriority.Low, TicketStatus.Resolved,
            "satis@helpdeskops.com", "it@helpdeskops.com", "DEMO-LT-003", 14, 72),
        ("Ekran kartı sürücüsü hata veriyor", "Harici monitör bağlayınca ekran siyaha dönüyor.",
            TicketCategory.Hardware, TicketPriority.High, TicketStatus.InProgress,
            "satis@helpdeskops.com", "it@helpdeskops.com", "DEMO-LT-003", 2, null),
        ("Yazıcı kağıt sıkıştırıyor", "Her çıktıda kağıt sıkışıyor, tepsiyi temizledim.",
            TicketCategory.Printer, TicketPriority.Medium, TicketStatus.Closed,
            "test@helpdeskops.com", "it@helpdeskops.com", "DEMO-PR-001", 18, 4),
        ("VPN bağlantısı kopuyor", "Evden çalışırken VPN birkaç dakikada bir kopuyor.",
            TicketCategory.Network, TicketPriority.Critical, TicketStatus.Resolved,
            "satis@helpdeskops.com", "it@helpdeskops.com", null, 10, 2),
        ("Muhasebe programı lisans hatası", "Program açılırken lisans süresi doldu uyarısı veriyor.",
            TicketCategory.Software, TicketPriority.Critical, TicketStatus.Closed,
            "test@helpdeskops.com", "it@helpdeskops.com", null, 7, 1),
        ("Yeni personel için e-posta hesabı", "Pazartesi başlayacak çalışan için Outlook hesabı açılması gerekiyor.",
            TicketCategory.EmailOutlook, TicketPriority.Medium, TicketStatus.Open,
            "ik@helpdeskops.com", null, null, 1, null),
        ("Monitör titriyor", "Monitör ara ara titriyor ve renkler bozuluyor.",
            TicketCategory.Hardware, TicketPriority.Low, TicketStatus.Open,
            "test@helpdeskops.com", null, "DEMO-MN-001", 0, null),
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
        await SeedTicketsAsync(services, userManager);
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

    private static async Task SeedTicketsAsync(IServiceProvider services, UserManager<ApplicationUser> userManager)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        foreach (var demo in DemoTickets)
        {
            var requester = await userManager.FindByEmailAsync(demo.RequesterEmail);
            if (requester == null || await context.Tickets.AnyAsync(t => t.Title == demo.Title && t.RequesterId == requester.Id))
                continue;

            var assignee = demo.AssigneeEmail != null ? await userManager.FindByEmailAsync(demo.AssigneeEmail) : null;
            var device = demo.DeviceSerial != null ? await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == demo.DeviceSerial) : null;

            var createdAt = DateTime.UtcNow.AddDays(-demo.CreatedDaysAgo).AddHours(-3);
            var resolvedAt = demo.ResolvedAfterHours is int hours ? createdAt.AddHours(hours) : (DateTime?)null;

            context.Tickets.Add(new Ticket
            {
                Title = demo.Title,
                Description = demo.Description,
                Category = demo.Category,
                Priority = demo.Priority,
                Status = demo.Status,
                RequesterId = requester.Id,
                AssignedToId = assignee?.Id,
                DeviceId = device?.Id,
                CreatedAt = createdAt,
                UpdatedAt = resolvedAt,
                ResolvedAt = resolvedAt
            });
        }

        await context.SaveChangesAsync();
    }
}