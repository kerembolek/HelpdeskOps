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

    // Matched by serial number, so devices added or edited by hand are left alone.
    // Purchase dates are picked so the demo has active, expiring-soon, and expired warranties.
    private static readonly (string Hostname, string Ip, string Serial, string Brand, string Model, DeviceType Type,
        int PurchasedDaysAgo, int WarrantyMonths, string? AssignedEmail)[] DemoDevices =
    {
        ("MUH-LT-001", "10.10.20.11", "DEMO-LT-001", "Lenovo", "ThinkPad E14", DeviceType.Laptop, 715, 24, "test@helpdeskops.com"),
        ("MUH-MN-001", "10.10.20.12", "DEMO-MN-001", "Dell", "P2422H", DeviceType.Monitor, 400, 36, "test@helpdeskops.com"),
        ("BT-DT-001", "10.10.10.21", "DEMO-DT-001", "HP", "EliteDesk 800 G6", DeviceType.Desktop, 500, 12, "it@helpdeskops.com"),
        ("MUH-PR-001", "10.10.20.50", "DEMO-PR-001", "HP", "LaserJet Pro M404dn", DeviceType.Printer, 340, 12, null),
        ("IK-LT-001", "10.10.30.11", "DEMO-LT-002", "Lenovo", "ThinkPad E14", DeviceType.Laptop, 200, 24, "ik@helpdeskops.com"),
        ("SAT-LT-001", "10.10.40.11", "DEMO-LT-003", "Lenovo", "ThinkPad E14", DeviceType.Laptop, 800, 24, "satis@helpdeskops.com"),
        ("BT-SW-001", "10.10.10.2", "DEMO-SW-001", "Cisco", "Catalyst 2960-X", DeviceType.Switch, 900, 60, null),
    };

    // Matched by device serial; only devices that are not patched yet get a port
    private static readonly (string DeviceSerial, string SwitchSerial, string Port, int Vlan)[] DemoPortLinks =
    {
        ("DEMO-DT-001", "DEMO-SW-001", "Gi1/0/2", 10),
        ("DEMO-LT-001", "DEMO-SW-001", "Gi1/0/5", 20),
        ("DEMO-LT-002", "DEMO-SW-001", "Gi1/0/10", 30),
        ("DEMO-LT-003", "DEMO-SW-001", "Gi1/0/14", 40),
        ("DEMO-PR-001", "DEMO-SW-001", "Gi1/0/24", 20),
    };

    // Service contact per brand; example.com addresses only
    private static readonly Dictionary<string, (string Name, string Phone, string Email)> DemoVendors = new()
    {
        ["Lenovo"] = ("Lenovo Yetkili Servis", "0212 555 01 10", "lenovo-servis@example.com"),
        ["Dell"] = ("Dell Teknik Destek", "0212 555 01 20", "dell-destek@example.com"),
        ["HP"] = ("HP Yetkili Servis", "0212 555 01 30", "hp-servis@example.com"),
        ["Cisco"] = ("Cisco İş Ortağı Destek", "0212 555 01 40", "cisco-destek@example.com"),
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

    // Matched by ticket title, author, and body. HoursAfterCreated places the comment in the ticket's timeline.
    private static readonly (string TicketTitle, string AuthorEmail, string Body, double HoursAfterCreated)[] DemoComments =
    {
        ("Ekran kartı sürücüsü hata veriyor", "satis@helpdeskops.com",
            "Sunum öncesi harici monitöre ihtiyacım var, öğleden sonra müsait misiniz?", 0.2),
        ("Ekran kartı sürücüsü hata veriyor", "it@helpdeskops.com",
            "Sürücüyü kaldırıp üreticinin son sürümünü kuracağım. 14:00'te masanıza geliyorum.", 0.6),
        ("Ekran kartı sürücüsü hata veriyor", "satis@helpdeskops.com",
            "Sürücü sonrası bir kez daha karardı ama yeniden başlatınca düzeldi.", 20),
        ("Laptop fanı çok ses yapıyor", "it@helpdeskops.com",
            "Fan ve soğutucu temizlendi, termal macun yenilendi. Birkaç gün kullanıp haber verir misiniz?", 26),
        ("Laptop fanı çok ses yapıyor", "ik@helpdeskops.com",
            "Teşekkürler, ses kesildi ve cihaz artık ısınmıyor.", 29),
    };

    // Matched by device serial, type, and description. Assignment entries for assigned demo devices are added separately.
    private static readonly (string DeviceSerial, DeviceEventType Type, string Description, int DaysAgo)[] DemoDeviceEvents =
    {
        ("DEMO-DT-001", DeviceEventType.HardwareUpgrade, "HDD yerine 512 GB SSD takıldı.", 60),
        ("DEMO-LT-003", DeviceEventType.HardwareUpgrade, "RAM 8 GB'tan 16 GB'a yükseltildi.", 40),
        ("DEMO-LT-001", DeviceEventType.PartReplacement, "Anakart garanti kapsamında değiştirildi.", 29),
        ("DEMO-LT-001", DeviceEventType.Format, "Windows 11 temiz kurulum yapıldı, kullanıcı verileri geri yüklendi.", 28),
        ("DEMO-LT-002", DeviceEventType.Repair, "Fan ve soğutucu temizlendi, termal macun yenilendi.", 20),
        ("DEMO-PR-001", DeviceEventType.PartReplacement, "Fuser ünitesi ve kağıt besleme silindiri değiştirildi.", 17),
        ("DEMO-LT-003", DeviceEventType.PartReplacement, "Batarya yenisiyle değiştirildi.", 11),
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
        await SeedPortLinksAsync(services);
        await SeedTicketsAsync(services, userManager);
        await SeedTicketHistoryAsync(services, userManager);
        await SeedCommentsAsync(services, userManager);
        await SeedDeviceEventsAsync(services, userManager);
    }

    private static async Task SeedDevicesAsync(IServiceProvider services, UserManager<ApplicationUser> userManager)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        foreach (var demo in DemoDevices)
        {
            var device = await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == demo.Serial);

            if (device == null)
            {
                var assignedUser = demo.AssignedEmail != null ? await userManager.FindByEmailAsync(demo.AssignedEmail) : null;

                device = new Device
                {
                    Hostname = demo.Hostname,
                    IpAddress = demo.Ip,
                    SerialNumber = demo.Serial,
                    Brand = demo.Brand,
                    Model = demo.Model,
                    DeviceType = demo.Type,
                    // Network gear is in use without being assigned to a person
                    Status = assignedUser != null || demo.Type == DeviceType.Switch ? DeviceStatus.Active : DeviceStatus.InStorage,
                    AssignedUserId = assignedUser?.Id,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };
                context.Devices.Add(device);
            }
            else if (device.PurchaseDate != null)
            {
                continue;
            }

            // New devices, and demo devices seeded before warranty details existed, get purchase and vendor data
            var purchaseDate = DateTime.Today.AddDays(-demo.PurchasedDaysAgo);
            device.PurchaseDate = purchaseDate;
            device.WarrantyEndDate = purchaseDate.AddMonths(demo.WarrantyMonths);
            if (DemoVendors.TryGetValue(demo.Brand, out var vendor))
            {
                device.VendorName = vendor.Name;
                device.VendorPhone = vendor.Phone;
                device.VendorEmail = vendor.Email;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedPortLinksAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        foreach (var demo in DemoPortLinks)
        {
            var device = await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == demo.DeviceSerial);
            var networkSwitch = await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == demo.SwitchSerial && d.DeviceType == DeviceType.Switch);
            if (device == null || networkSwitch == null || device.SwitchId != null
                || await context.Devices.AnyAsync(d => d.SwitchId == networkSwitch.Id && d.SwitchPort == demo.Port))
                continue;

            device.SwitchId = networkSwitch.Id;
            device.SwitchPort = demo.Port;
            device.VlanId = demo.Vlan;
            await context.SaveChangesAsync();
        }
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

    // Rebuilds a plausible history for demo tickets that have none yet:
    // created -> assigned and in progress after 30 minutes -> resolved or closed at ResolvedAt
    private static async Task SeedTicketHistoryAsync(IServiceProvider services, UserManager<ApplicationUser> userManager)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        foreach (var demo in DemoTickets)
        {
            var requester = await userManager.FindByEmailAsync(demo.RequesterEmail);
            if (requester == null)
                continue;

            var ticket = await context.Tickets
                .Include(t => t.AssignedTo)
                .FirstOrDefaultAsync(t => t.Title == demo.Title && t.RequesterId == requester.Id);
            if (ticket == null || await context.TicketActivities.AnyAsync(a => a.TicketId == ticket.Id))
                continue;

            void Add(string? actorId, TicketActivityType type, string? oldValue, string? newValue, DateTime at) =>
                context.TicketActivities.Add(new TicketActivity
                {
                    TicketId = ticket.Id,
                    ActorId = actorId,
                    Type = type,
                    OldValue = oldValue,
                    NewValue = newValue,
                    CreatedAt = at
                });

            Add(requester.Id, TicketActivityType.Created, null, null, ticket.CreatedAt);

            if (ticket.AssignedTo != null)
            {
                var takenAt = ticket.CreatedAt.AddMinutes(30);
                Add(ticket.AssignedToId, TicketActivityType.AssigneeChanged,
                    "Atanmadı", $"{ticket.AssignedTo.FirstName} {ticket.AssignedTo.LastName}", takenAt);

                if (ticket.Status != TicketStatus.Open)
                {
                    Add(ticket.AssignedToId, TicketActivityType.StatusChanged,
                        TicketStatus.Open.GetDisplayName(), TicketStatus.InProgress.GetDisplayName(), takenAt);
                }

                if (ticket.ResolvedAt is DateTime resolvedAt)
                {
                    Add(ticket.AssignedToId, TicketActivityType.StatusChanged,
                        TicketStatus.InProgress.GetDisplayName(), ticket.Status.GetDisplayName(), resolvedAt);
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedCommentsAsync(IServiceProvider services, UserManager<ApplicationUser> userManager)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        foreach (var demo in DemoComments)
        {
            var author = await userManager.FindByEmailAsync(demo.AuthorEmail);
            var ticket = await context.Tickets.FirstOrDefaultAsync(t => t.Title == demo.TicketTitle);
            if (author == null || ticket == null
                || await context.TicketComments.AnyAsync(c => c.TicketId == ticket.Id && c.AuthorId == author.Id && c.Body == demo.Body))
                continue;

            context.TicketComments.Add(new TicketComment
            {
                TicketId = ticket.Id,
                AuthorId = author.Id,
                Body = demo.Body,
                CreatedAt = ticket.CreatedAt.AddHours(demo.HoursAfterCreated)
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedDeviceEventsAsync(IServiceProvider services, UserManager<ApplicationUser> userManager)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var itAdmin = await userManager.FindByEmailAsync("it@helpdeskops.com");

        async Task AddIfMissing(Device device, DeviceEventType type, string description, int daysAgo)
        {
            if (await context.DeviceEvents.AnyAsync(e => e.DeviceId == device.Id && e.Type == type && e.Description == description))
                return;

            context.DeviceEvents.Add(new DeviceEvent
            {
                DeviceId = device.Id,
                Type = type,
                Description = description,
                EventDate = DateTime.UtcNow.AddDays(-daysAgo),
                PerformedById = itAdmin?.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        // The first assignment of each assigned demo device, before its maintenance entries
        foreach (var demo in DemoDevices.Where(d => d.AssignedEmail != null))
        {
            var device = await context.Devices.Include(d => d.AssignedUser).FirstOrDefaultAsync(d => d.SerialNumber == demo.Serial);
            if (device?.AssignedUser != null)
            {
                await AddIfMissing(device, DeviceEventType.AssignmentChanged,
                    $"Zimmetlendi: {device.AssignedUser.FirstName} {device.AssignedUser.LastName}", 90);
            }
        }

        foreach (var demo in DemoDeviceEvents)
        {
            var device = await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == demo.DeviceSerial);
            if (device != null)
            {
                await AddIfMissing(device, demo.Type, demo.Description, demo.DaysAgo);
            }
        }

        await context.SaveChangesAsync();
    }
}