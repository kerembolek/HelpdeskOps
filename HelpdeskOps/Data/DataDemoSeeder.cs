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
        ("mudur@helpdeskops.com", "Selin", "Arslan", "Yönetim", "Manager"),
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
    private static readonly (string Title, string Description, TicketCategory Category, TicketSubcategory Subcategory, string? OtherText,
        TicketPriority Priority, TicketStatus Status,
        string RequesterEmail, string? AssigneeEmail, string? DeviceSerial, int CreatedDaysAgo, int? ResolvedAfterHours)[] DemoTickets =
    {
        ("Dizüstü bilgisayar açılmıyor", "Güç tuşuna basınca ışık yanıyor ama ekran gelmiyor.",
            TicketCategory.Hardware, TicketSubcategory.PcWontStart, null, TicketPriority.High, TicketStatus.Closed,
            "test@helpdeskops.com", "it@helpdeskops.com", "DEMO-LT-001", 30, 6),
        ("Laptop fanı çok ses yapıyor", "Toplantılarda fan sesi çok yüksek, cihaz da ısınıyor.",
            TicketCategory.Hardware, TicketSubcategory.Other, "Fan sesi ve ısınma", TicketPriority.Medium, TicketStatus.Resolved,
            "ik@helpdeskops.com", "it@helpdeskops.com", "DEMO-LT-002", 21, 30),
        ("Batarya çabuk bitiyor", "Tam şarjla bir saat bile dayanmıyor.",
            TicketCategory.Hardware, TicketSubcategory.PartFailure, null, TicketPriority.Low, TicketStatus.Resolved,
            "satis@helpdeskops.com", "it@helpdeskops.com", "DEMO-LT-003", 14, 72),
        ("Ekran kartı sürücüsü hata veriyor", "Harici monitör bağlayınca ekran siyaha dönüyor.",
            TicketCategory.Hardware, TicketSubcategory.Display, null, TicketPriority.High, TicketStatus.InProgress,
            "satis@helpdeskops.com", "it@helpdeskops.com", "DEMO-LT-003", 2, null),
        ("Yazıcı kağıt sıkıştırıyor", "Her çıktıda kağıt sıkışıyor, tepsiyi temizledim.",
            TicketCategory.Printer, TicketSubcategory.PaperJam, null, TicketPriority.Medium, TicketStatus.Closed,
            "test@helpdeskops.com", "it@helpdeskops.com", "DEMO-PR-001", 18, 4),
        ("VPN bağlantısı kopuyor", "Evden çalışırken VPN birkaç dakikada bir kopuyor.",
            TicketCategory.Network, TicketSubcategory.Vpn, null, TicketPriority.Critical, TicketStatus.Resolved,
            "satis@helpdeskops.com", "it@helpdeskops.com", null, 10, 2),
        ("Muhasebe programı lisans hatası", "Program açılırken lisans süresi doldu uyarısı veriyor.",
            TicketCategory.Software, TicketSubcategory.License, null, TicketPriority.Critical, TicketStatus.Closed,
            "test@helpdeskops.com", "it@helpdeskops.com", null, 7, 1),
        ("Yeni personel için e-posta hesabı", "Pazartesi başlayacak çalışan için Outlook hesabı açılması gerekiyor.",
            TicketCategory.EmailOutlook, TicketSubcategory.NewMailbox, null, TicketPriority.Medium, TicketStatus.Open,
            "ik@helpdeskops.com", null, null, 1, null),
        ("Monitör titriyor", "Monitör ara ara titriyor ve renkler bozuluyor.",
            TicketCategory.Hardware, TicketSubcategory.Display, null, TicketPriority.Low, TicketStatus.Open,
            "test@helpdeskops.com", null, "DEMO-MN-001", 0, null),
    };

    // Satisfaction ratings for resolved demo tickets, given a day after resolution; matched by ticket title
    private static readonly (string TicketTitle, int Rating, string? Comment)[] DemoRatings =
    {
        ("Dizüstü bilgisayar açılmıyor", 5, "Aynı gün çözüldü, çok teşekkürler."),
        ("Laptop fanı çok ses yapıyor", 3, "Çözüldü ama biraz uzun sürdü."),
        ("Batarya çabuk bitiyor", 4, null),
        ("VPN bağlantısı kopuyor", 5, "Evden çalışırken hayat kurtardınız."),
        ("Muhasebe programı lisans hatası", 4, null),
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

    // Matched by title. Counters give the solution bank management page some self-service numbers to show.
    private static readonly (string Title, TicketCategory Category, string Symptoms, string[] Steps, int Solved, int Escalated)[] DemoSolutions =
    {
        ("Monitör görüntü vermiyor", TicketCategory.Hardware,
            "Bilgisayar açık ama ekran siyah veya \"Sinyal yok\" yazıyor.",
            new[]
            {
                "Monitörün güç ışığının yandığını kontrol edin; yanmıyorsa güç kablosunu ve prizi kontrol edin.",
                "Görüntü kablosunun (HDMI/DisplayPort) hem monitöre hem bilgisayara tam oturduğunu kontrol edin.",
                "Monitörün menü tuşundan doğru girişin (HDMI/DisplayPort) seçili olduğundan emin olun.",
                "Dizüstü kullanıyorsanız Windows + P tuşlarına basıp \"Çoğalt\" veya \"Genişlet\" seçin."
            }, 4, 1),
        ("Bilgisayar çok yavaş çalışıyor", TicketCategory.Hardware,
            "Programlar geç açılıyor, fare takılıyor.",
            new[]
            {
                "Kullanmadığınız programları ve tarayıcı sekmelerini kapatın.",
                "Ctrl + Shift + Esc ile Görev Yöneticisi'ni açıp CPU veya Bellek kullanımı yüksek olan programa bakın.",
                "Bilgisayarı tamamen yeniden başlatın (Başlat > Güç > Yeniden başlat).",
                "Windows Update'te bekleyen güncelleme varsa mesai sonunda yükleyin."
            }, 3, 4),
        ("Yazıcı kağıt sıkıştırıyor", TicketCategory.Printer,
            "Çıktı yarıda kalıyor, yazıcı ekranında kağıt sıkışması uyarısı var.",
            new[]
            {
                "Yazıcıyı kapatın ve fişini çekin.",
                "Ön kapağı ve kağıt tepsisini açıp sıkışan kağıdı iki elinizle, kağıdın çıkış yönünde yavaşça çekin.",
                "Tepsideki kağıtları havalandırıp düzgün bir deste halinde geri koyun; tepsiyi aşırı doldurmayın.",
                "Yazıcıyı açıp bir deneme sayfası yazdırın."
            }, 6, 2),
        ("Yazıcı listede görünmüyor", TicketCategory.Printer,
            "Yazdır dediğinizde yazıcı seçeneklerde çıkmıyor.",
            new[]
            {
                "Yazıcının açık olduğunu ve ekranında hata olmadığını kontrol edin.",
                "Ayarlar > Bluetooth ve cihazlar > Yazıcılar ve tarayıcılar ekranını açın.",
                "\"Cihaz ekle\" deyip listede çıkan ofis yazıcısını seçin.",
                "Bilgisayarı yeniden başlatıp tekrar yazdırmayı deneyin."
            }, 3, 1),
        ("İnternete bağlanamıyorum", TicketCategory.Network,
            "Tarayıcıda sayfalar açılmıyor, ağ simgesinde ünlem işareti var.",
            new[]
            {
                "Kablolu bağlanıyorsanız ağ kablosunun bilgisayara ve duvardaki prize tam oturduğunu kontrol edin.",
                "Kablosuz bağlanıyorsanız Wi-Fi'ı kapatıp açın ve şirket ağına yeniden bağlanın.",
                "Bilgisayarı yeniden başlatın.",
                "Yan masadaki arkadaşınızın internetinin çalışıp çalışmadığını kontrol edin; çalışmıyorsa genel bir arıza olabilir, talep açın."
            }, 4, 3),
        ("Outlook e-posta göndermiyor", TicketCategory.EmailOutlook,
            "Gönderilen e-postalar Giden Kutusu'nda bekliyor.",
            new[]
            {
                "Outlook'un sağ alt köşesinde \"Bağlantı kesildi\" veya \"Çevrimdışı çalışma\" yazısı olup olmadığına bakın.",
                "Gönder/Al sekmesinde \"Çevrimdışı Çalış\" seçiliyse kapatın.",
                "Giden Kutusu'ndaki büyük ekli e-postayı açıp eki küçültün veya paylaşım bağlantısı olarak gönderin.",
                "Outlook'u kapatıp yeniden açın."
            }, 5, 1),
        ("Program donuyor veya yanıt vermiyor", TicketCategory.Software,
            "Program açık ama tıklamalara tepki vermiyor.",
            new[]
            {
                "Birkaç dakika bekleyin; büyük dosyalarda program geçici olarak yanıt vermeyebilir.",
                "Ctrl + Shift + Esc ile Görev Yöneticisi'ni açın, programı seçip \"Görevi sonlandır\" deyin.",
                "Programı yeniden açın ve son çalıştığınız dosyayı kontrol edin.",
                "Sorun tekrarlıyorsa bilgisayarı yeniden başlatın."
            }, 2, 2),
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
        await SeedAssignmentFormsAsync(services, userManager);
        await SeedTicketsAsync(services, userManager);
        await SeedTicketHistoryAsync(services, userManager);
        await SeedCommentsAsync(services, userManager);
        await SeedRatingsAsync(services);
        await SeedNotificationsAsync(services, userManager);
        await SeedDeviceEventsAsync(services, userManager);
        await SeedSolutionsAsync(services);
    }

    // A few unread notifications so the navbar count shows up; only for demo users who have none yet
    private static async Task SeedNotificationsAsync(IServiceProvider services, UserManager<ApplicationUser> userManager)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        var demo = new (string Email, string TicketTitle, string Message)[]
        {
            ("test@helpdeskops.com", "Monitör titriyor", "BT ekibi yeni bir yorum yazdı."),
            ("test@helpdeskops.com", "Dizüstü bilgisayar açılmıyor", "talebinizin durumu \"Kapatıldı\" oldu."),
            ("it@helpdeskops.com", "Ekran kartı sürücüsü hata veriyor", "talep size atandı.")
        };

        foreach (var group in demo.GroupBy(d => d.Email))
        {
            var user = await userManager.FindByEmailAsync(group.Key);
            if (user == null || await context.Notifications.AnyAsync(n => n.UserId == user.Id))
                continue;

            foreach (var item in group)
            {
                var ticket = await context.Tickets.FirstOrDefaultAsync(t => t.Title == item.TicketTitle);
                if (ticket == null)
                    continue;

                context.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Message = $"#{ticket.Id} \"{ticket.Title}\": {item.Message}",
                    Url = $"/Tickets/Details/{ticket.Id}",
                    CreatedAt = DateTime.UtcNow.AddHours(-1)
                });
            }
        }

        await context.SaveChangesAsync();
    }

    // Only resolved, still unrated demo tickets get a rating, so ratings given by hand are kept
    private static async Task SeedRatingsAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        foreach (var demo in DemoRatings)
        {
            var ticket = await context.Tickets.FirstOrDefaultAsync(t => t.Title == demo.TicketTitle
                && t.ResolvedAt != null && t.SatisfactionRating == null);
            if (ticket == null)
                continue;

            ticket.SatisfactionRating = demo.Rating;
            ticket.SatisfactionComment = demo.Comment;
            ticket.RatedAt = ticket.ResolvedAt!.Value.AddDays(1);
            context.TicketActivities.Add(new TicketActivity
            {
                TicketId = ticket.Id,
                ActorId = ticket.RequesterId,
                Type = TicketActivityType.Rated,
                NewValue = $"{demo.Rating}/5",
                CreatedAt = ticket.RatedAt.Value
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedSolutionsAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        foreach (var demo in DemoSolutions)
        {
            if (await context.SolutionArticles.AnyAsync(a => a.Title == demo.Title))
                continue;

            context.SolutionArticles.Add(new SolutionArticle
            {
                Title = demo.Title,
                Category = demo.Category,
                Symptoms = demo.Symptoms,
                Steps = string.Join("\n", demo.Steps),
                IsPublished = true,
                SolvedCount = demo.Solved,
                EscalatedCount = demo.Escalated,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
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
            var purchaseDate = TurkeyTime.Today.AddDays(-demo.PurchasedDaysAgo);
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

    // Demo devices that are assigned to someone but have no zimmet form yet get one issued by the IT demo account.
    // The monitor's form is left unconfirmed so "Cihazlarım" shows the "Teslim Aldım" button.
    private static async Task SeedAssignmentFormsAsync(IServiceProvider services, UserManager<ApplicationUser> userManager)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var issuer = await userManager.FindByEmailAsync("it@helpdeskops.com");

        foreach (var demo in DemoDevices.Where(d => d.AssignedEmail != null))
        {
            var device = await context.Devices
                .Include(d => d.AssignedUser)
                .FirstOrDefaultAsync(d => d.SerialNumber == demo.Serial);
            if (device?.AssignedUser == null || await context.AssignmentForms.AnyAsync(f => f.DeviceId == device.Id))
                continue;

            var user = device.AssignedUser;
            var issuedAt = DateTime.UtcNow.AddDays(-Math.Min(demo.PurchasedDaysAgo, 180));
            var brandModel = string.Join(" ", new[] { device.Brand, device.Model }.Where(s => !string.IsNullOrWhiteSpace(s)));

            context.AssignmentForms.Add(new AssignmentForm
            {
                DeviceId = device.Id,
                UserId = user.Id,
                IssuedById = issuer?.Id,
                Hostname = device.Hostname,
                SerialNumber = device.SerialNumber,
                BrandModel = brandModel.Length > 0 ? brandModel : null,
                DeviceTypeName = device.DeviceType.GetDisplayName(),
                EmployeeName = $"{user.FirstName} {user.LastName}",
                Department = user.Department,
                IssuedByName = issuer != null ? $"{issuer.FirstName} {issuer.LastName}" : null,
                IssuedAt = issuedAt,
                AcceptedAt = device.DeviceType == DeviceType.Monitor ? null : issuedAt.AddHours(2)
            });
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
            if (requester == null)
                continue;

            // Demo tickets seeded before subcategories existed get theirs filled in
            var existing = await context.Tickets.FirstOrDefaultAsync(t => t.Title == demo.Title && t.RequesterId == requester.Id);
            if (existing != null)
            {
                if (existing.Subcategory == null && existing.Category == demo.Category)
                {
                    existing.Subcategory = demo.Subcategory;
                    existing.OtherCategoryText = demo.OtherText;
                }
                continue;
            }

            var assignee = demo.AssigneeEmail != null ? await userManager.FindByEmailAsync(demo.AssigneeEmail) : null;
            var device = demo.DeviceSerial != null ? await context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == demo.DeviceSerial) : null;

            var createdAt = DateTime.UtcNow.AddDays(-demo.CreatedDaysAgo).AddHours(-3);
            var resolvedAt = demo.ResolvedAfterHours is int hours ? createdAt.AddHours(hours) : (DateTime?)null;

            context.Tickets.Add(new Ticket
            {
                Title = demo.Title,
                Description = demo.Description,
                Category = demo.Category,
                Subcategory = demo.Subcategory,
                OtherCategoryText = demo.OtherText,
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