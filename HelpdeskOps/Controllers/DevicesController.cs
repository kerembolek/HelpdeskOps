
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HelpdeskOps;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Data;
using HelpdeskOps.Services;
using Microsoft.AspNetCore.Authorization;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Claims;
using System.Text;

[Authorize(Roles = "ITAdmin")]

public class DevicesController : Controller
{
    private readonly ApplicationDbContext _context;

    public DevicesController(ApplicationDbContext context)
    {
        _context = context;
    }

    private async Task LoadUsersAsync(string? selectedId = null)
    {
        var users = await _context.Users
            .Where(u => u.AnonymizedAt == null)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new { u.Id, FullName = u.FirstName + " " + u.LastName + (u.Department != null ? " (" + u.Department + ")" : "") })
            .ToListAsync();
        ViewBag.Users = new SelectList(users, "Id", "FullName", selectedId);
    }

    // Switches a device can be patched into; a switch can't be patched into itself
    private async Task LoadSwitchesAsync(int? selectedId = null, int? excludeId = null)
    {
        var switches = await _context.Devices
            .Where(d => d.DeviceType == DeviceType.Switch && d.Id != excludeId)
            .OrderBy(d => d.Hostname)
            .Select(d => new { d.Id, Name = d.Hostname + " (" + d.IpAddress + ")" })
            .ToListAsync();
        ViewBag.Switches = new SelectList(switches, "Id", "Name", selectedId);
    }

    private async Task LoadFormListsAsync(Device device)
    {
        await LoadUsersAsync(device.AssignedUserId);
        await LoadSwitchesAsync(device.SwitchId, device.Id == 0 ? null : device.Id);
    }

    // The dropdown only lists switches, but the posted id is checked here as well
    private async Task ValidateSwitchPortAsync(Device device)
    {
        device.SwitchPort = string.IsNullOrWhiteSpace(device.SwitchPort) ? null : device.SwitchPort.Trim();

        if (device.SwitchId == null)
        {
            if (device.SwitchPort != null)
                ModelState.AddModelError(nameof(Device.SwitchId), "Port girildiyse switch seçilmelidir.");
            if (device.VlanId != null)
                ModelState.AddModelError(nameof(Device.SwitchId), "VLAN girildiyse switch seçilmelidir.");
        }
        else
        {
            var target = await _context.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == device.SwitchId);
            if (target == null || target.DeviceType != DeviceType.Switch || target.Id == device.Id)
            {
                ModelState.AddModelError(nameof(Device.SwitchId), "Seçilen switch bulunamadı.");
            }
            else if (device.SwitchPort == null)
            {
                ModelState.AddModelError(nameof(Device.SwitchPort), "Switch seçildiyse port zorunludur.");
            }
            else
            {
                var occupant = await _context.Devices.AsNoTracking()
                    .Where(d => d.SwitchId == device.SwitchId && d.SwitchPort == device.SwitchPort && d.Id != device.Id)
                    .Select(d => d.Hostname)
                    .FirstOrDefaultAsync();
                if (occupant != null)
                    ModelState.AddModelError(nameof(Device.SwitchPort), $"Bu port zaten {occupant} cihazına bağlı.");
            }
        }

        // A switch with patched devices has to stay a switch
        if (device.Id != 0 && device.DeviceType != DeviceType.Switch
            && await _context.Devices.AnyAsync(d => d.SwitchId == device.Id))
        {
            ModelState.AddModelError(nameof(Device.DeviceType), "Bu switch'e bağlı cihazlar var. Türünü değiştirmeden önce bağlantıları kaldırın.");
        }
    }

    private async Task<string> PortLabelAsync(int? switchId, string? port)
    {
        if (switchId == null)
        {
            return "Bağlı değil";
        }

        var hostname = await _context.Devices.Where(d => d.Id == switchId).Select(d => d.Hostname).FirstOrDefaultAsync();
        return $"{hostname ?? "-"} {port}";
    }

    // Zimmet için seçilen kullanıcının gerçekten var olduğunu doğrular
    private async Task ValidateAssignedUserAsync(string? userId)
    {
        if (!string.IsNullOrEmpty(userId) && !await _context.Users.AnyAsync(u => u.Id == userId && u.AnonymizedAt == null))
        {
            ModelState.AddModelError(nameof(Device.AssignedUserId), "Seçilen kullanıcı bulunamadı.");
        }
    }

    private void ValidateWarrantyDates(Device device)
    {
        if (device.PurchaseDate?.Date > TurkeyTime.Today)
        {
            ModelState.AddModelError(nameof(Device.PurchaseDate), "Satın alma tarihi ileri bir tarih olamaz.");
        }
        if (device.PurchaseDate != null && device.WarrantyEndDate?.Date < device.PurchaseDate.Value.Date)
        {
            ModelState.AddModelError(nameof(Device.WarrantyEndDate), "Garanti bitişi satın alma tarihinden önce olamaz.");
        }
    }

    // Event types IT staff can enter by hand; assignment, status, and port changes are logged automatically
    private static readonly DeviceEventType[] ManualEventTypes =
    {
        DeviceEventType.Format, DeviceEventType.HardwareUpgrade, DeviceEventType.PartReplacement,
        DeviceEventType.Repair, DeviceEventType.Other
    };

    private void LogEvent(Device device, DeviceEventType type, string description)
    {
        _context.DeviceEvents.Add(new DeviceEvent
        {
            Device = device,
            Type = type,
            Description = description,
            EventDate = DateTime.UtcNow,
            PerformedById = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<string> UserNameAsync(string? userId)
    {
        if (userId == null)
        {
            return "Depo (atanmamış)";
        }

        var user = await _context.Users.FindAsync(userId);
        return user != null ? $"{user.FirstName} {user.LastName}" : "-";
    }

    // Closes the device's open zimmet form and, when the device goes to a person,
    // opens a new one with a copy of the device and person details as they are now
    private async Task UpdateAssignmentFormsAsync(Device device, string? newUserId)
    {
        var now = DateTime.UtcNow;
        if (device.Id != 0)
        {
            var openForms = await _context.AssignmentForms
                .Where(f => f.DeviceId == device.Id && f.ReturnedAt == null)
                .ToListAsync();
            foreach (var form in openForms)
            {
                form.ReturnedAt = now;
            }
        }

        var user = newUserId != null ? await _context.Users.FindAsync(newUserId) : null;
        if (user == null)
        {
            return;
        }

        var issuerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var issuer = issuerId != null ? await _context.Users.FindAsync(issuerId) : null;
        var brandModel = string.Join(" ", new[] { device.Brand, device.Model }.Where(s => !string.IsNullOrWhiteSpace(s)));

        _context.AssignmentForms.Add(new AssignmentForm
        {
            Device = device,
            UserId = user.Id,
            IssuedById = issuer?.Id,
            Hostname = device.Hostname,
            SerialNumber = device.SerialNumber,
            BrandModel = brandModel.Length > 0 ? brandModel : null,
            DeviceTypeName = device.DeviceType.GetDisplayName(),
            EmployeeName = $"{user.FirstName} {user.LastName}",
            Department = user.Department,
            IssuedByName = issuer != null ? $"{issuer.FirstName} {issuer.LastName}" : null,
            IssuedAt = now
        });
    }

    private void LoadEventTypes()
    {
        ViewBag.EventTypes = ManualEventTypes
            .Select(t => new SelectListItem(t.GetDisplayName(), ((int)t).ToString()))
            .ToList();
    }

    // GET: DEVICES/Index?warranty=&page=2
    // warranty: "expiring" (ends within WarrantyExpiringSoonDays) or "expired"; retired devices are left out of both
    public async Task<IActionResult> Index(string? warranty, int page = 1)
    {
        var devices = _context.Devices.AsNoTracking().Include(d => d.AssignedUser).AsQueryable();
        var today = TurkeyTime.Today;

        switch (warranty)
        {
            case "expiring":
                var soon = today.AddDays(Device.WarrantyExpiringSoonDays);
                devices = devices.Where(d => d.Status != DeviceStatus.Retired
                    && d.WarrantyEndDate >= today && d.WarrantyEndDate <= soon)
                    .OrderBy(d => d.WarrantyEndDate).ThenBy(d => d.Id);
                break;
            case "expired":
                devices = devices.Where(d => d.Status != DeviceStatus.Retired && d.WarrantyEndDate < today)
                    .OrderByDescending(d => d.WarrantyEndDate).ThenBy(d => d.Id);
                break;
            default:
                warranty = null;
                devices = devices.OrderBy(d => d.Hostname).ThenBy(d => d.Id);
                break;
        }

        ViewBag.Warranty = warranty;
        ViewBag.PendingReturns = await _context.AssignmentForms
            .AsNoTracking()
            .Where(f => f.ReturnRequestedAt != null && f.ReturnedAt == null)
            .OrderBy(f => f.ReturnRequestedAt)
            .ToListAsync();
        return View(await PagedList<Device>.CreateAsync(devices, page, PageSize));
    }

    private const int PageSize = 25;

    private const int PingTimeoutMs = 1000;
    private const int PingBatchLimit = 100;

    // GET: DEVICES/PingStatus?ids=1&ids=2
    // Returns { "1": true, "2": false }. Pings run in parallel so one offline device does not hold up the rest.
    public async Task<IActionResult> PingStatus([FromQuery] int[] ids)
    {
        ids = ids.Distinct().Take(PingBatchLimit).ToArray();

        var devices = await _context.Devices.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new { d.Id, d.IpAddress })
            .ToListAsync();

        var results = await Task.WhenAll(devices.Select(async d => (d.Id, Online: await IsReachableAsync(d.IpAddress))));
        return Json(results.ToDictionary(r => r.Id.ToString(), r => r.Online));
    }

    private static async Task<bool> IsReachableAsync(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var address))
        {
            return false;
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, PingTimeoutMs);
            return reply.Status == IPStatus.Success;
        }
        catch (PingException)
        {
            return false;
        }
    }

    // GET: DEVICES/Labels?ids=1&ids=2
    // A printable sheet of QR stickers; without ids it prints every device that is still in use
    public async Task<IActionResult> Labels([FromQuery] int[] ids)
    {
        var devices = _context.Devices.AsNoTracking();
        devices = ids.Length > 0
            ? devices.Where(d => ids.Contains(d.Id))
            : devices.Where(d => d.Status != DeviceStatus.Retired);

        var labels = (await devices.OrderBy(d => d.Hostname).ThenBy(d => d.Id).ToListAsync())
            .Select(d => new DeviceLabel(d, QrCode.Svg(Url.Action("Device", "Scan", new { id = d.Id }, Request.Scheme)!)))
            .ToList();
        return View(labels);
    }

    // GET: DEVICES/Rdp/5
    // Downloads a .rdp file; Windows opens it with Remote Desktop Connection
    public async Task<IActionResult> Rdp(int id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null || !device.SupportsRemoteDesktop())
        {
            return NotFound();
        }

        var content = $"full address:s:{device.IpAddress}\r\nprompt for credentials:i:1\r\n";
        var fileName = string.Concat(device.Hostname.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)) + ".rdp";
        return File(Encoding.UTF8.GetBytes(content), "application/x-rdp", fileName);
    }

    // GET: DEVICES/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var device = await _context.Devices
            .Include(d => d.AssignedUser)
            .Include(d => d.Switch)
            .Include(d => d.ConnectedDevices)
                .ThenInclude(c => c.AssignedUser)
            .Include(d => d.Events.OrderByDescending(e => e.EventDate).ThenByDescending(e => e.Id))
                .ThenInclude(e => e.PerformedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id);
        if (device == null)
        {
            return NotFound();
        }

        ViewBag.AssignmentForms = await _context.AssignmentForms
            .AsNoTracking()
            .Where(f => f.DeviceId == id)
            .OrderByDescending(f => f.IssuedAt)
            .ToListAsync();

        LoadEventTypes();
        return View(device);
    }

    // POST: DEVICES/AddEvent/5
    // eventDate comes from a date input in the user's local time
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEvent(int id, DeviceEventType type, DateTime? eventDate, string? description)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        description = description?.Trim();
        var date = eventDate?.Date ?? TurkeyTime.Today;

        string? error = null;
        if (!ManualEventTypes.Contains(type))
            error = "Geçersiz işlem türü.";
        else if (string.IsNullOrEmpty(description))
            error = "Açıklama zorunludur.";
        else if (description.Length > 500)
            error = "Açıklama en fazla 500 karakter olabilir.";
        else if (date > TurkeyTime.Today)
            error = "İşlem tarihi ileri bir tarih olamaz.";

        if (error != null)
        {
            TempData["EventError"] = error;
            TempData["EventDraft"] = description;
        }
        else
        {
            // Noon Turkey time keeps the stored UTC value on the same calendar day
            var eventDateUtc = date == TurkeyTime.Today
                ? DateTime.UtcNow
                : TurkeyTime.ToUtc(date.AddHours(12));

            _context.DeviceEvents.Add(new DeviceEvent
            {
                DeviceId = id,
                Type = type,
                Description = description!,
                EventDate = eventDateUtc,
                PerformedById = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedAt = DateTime.UtcNow
            });
            device.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Redirect(Url.Action(nameof(Details), new { id }) + "#history");
    }

    // GET: DEVICES/Create
    public async Task<IActionResult> Create()
    {
        await LoadUsersAsync();
        await LoadSwitchesAsync();
        return View();
    }

    // POST: DEVICES/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Hostname,IpAddress,SerialNumber,Brand,Model,DeviceType,PurchaseDate,WarrantyEndDate,VendorName,VendorPhone,VendorEmail,Status,AssignedUserId,SwitchId,SwitchPort,VlanId")] Device device)
    {
        await ValidateAssignedUserAsync(device.AssignedUserId);
        ValidateWarrantyDates(device);
        await ValidateSwitchPortAsync(device);

        if (ModelState.IsValid)
        {
            // Timestamps are set by the server, not by the form
            device.CreatedAt = DateTime.UtcNow;
            device.LastUpdated = DateTime.UtcNow;
            device.AssignedUserId = string.IsNullOrEmpty(device.AssignedUserId) ? null : device.AssignedUserId;
            device.VendorName = device.VendorName?.Trim();
            device.VendorPhone = device.VendorPhone?.Trim();
            device.VendorEmail = device.VendorEmail?.Trim();

            _context.Add(device);
            if (device.AssignedUserId != null)
            {
                LogEvent(device, DeviceEventType.AssignmentChanged,
                    $"Zimmetlendi: {await UserNameAsync(device.AssignedUserId)}");
                await UpdateAssignmentFormsAsync(device, device.AssignedUserId);
            }
            if (device.SwitchId != null)
            {
                LogEvent(device, DeviceEventType.PortChanged,
                    $"Bağlandı: {await PortLabelAsync(device.SwitchId, device.SwitchPort)}");
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await LoadFormListsAsync(device);
        return View(device);
    }

    // GET: DEVICES/ImportTemplate
    // A header row and two sample devices to fill in
    public IActionResult ImportTemplate()
    {
        var samples = new (Device, string?)[]
        {
            (new Device
            {
                Hostname = "MUH-LT-010", IpAddress = "10.10.20.110", SerialNumber = "ORNEK-0001", Brand = "Lenovo", Model = "ThinkPad E14",
                DeviceType = DeviceType.Laptop, Status = DeviceStatus.Active,
                PurchaseDate = TurkeyTime.Today.AddMonths(-1), WarrantyEndDate = TurkeyTime.Today.AddMonths(23)
            }, "calisan@ornek.com"),
            (new Device
            {
                Hostname = "DEPO-MN-005", IpAddress = "10.10.90.15", SerialNumber = "ORNEK-0002", Brand = "Dell", Model = "P2422H",
                DeviceType = DeviceType.Monitor, Status = DeviceStatus.InStorage
            }, null)
        };

        return File(DeviceCsvImporter.ToCsv(samples), "text/csv; charset=utf-8", "cihaz-ice-aktarma-sablonu.csv");
    }

    // GET: DEVICES/Export
    // Every device in the import format, with the assignee's email
    public async Task<IActionResult> Export()
    {
        var devices = await _context.Devices
            .AsNoTracking()
            .Include(d => d.AssignedUser)
            .OrderBy(d => d.Hostname)
            .ToListAsync();

        var csv = DeviceCsvImporter.ToCsv(devices.Select(d => (d, d.AssignedUser?.Email)));
        return File(csv, "text/csv; charset=utf-8", $"cihazlar-{TurkeyTime.Now:yyyyMMdd}.csv");
    }

    // GET: DEVICES/Import
    public IActionResult Import()
    {
        return View(new List<string>());
    }

    // POST: DEVICES/Import
    // All or nothing: if any row has a problem, no device is added and every problem is listed
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(DeviceCsvImporter.MaxFileSize + 64 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return View(new List<string> { "Lütfen bir CSV dosyası seçin." });
        if (file.Length > DeviceCsvImporter.MaxFileSize)
            return View(new List<string> { "Dosya en fazla 1 MB olabilir." });
        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return View(new List<string> { "Yalnızca .csv dosyası yüklenebilir." });

        string text;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync();
        }

        var result = DeviceCsvImporter.Parse(text);

        // Checks against existing data
        var serials = result.Rows.Select(r => r.Device.SerialNumber).ToList();
        var existingSerials = await _context.Devices
            .Where(d => serials.Contains(d.SerialNumber))
            .Select(d => d.SerialNumber)
            .ToListAsync();
        var emails = result.Rows.Where(r => r.AssignedEmail != null).Select(r => r.AssignedEmail!.ToUpperInvariant()).Distinct().ToList();
        var usersByEmail = await _context.Users
            .Where(u => u.NormalizedEmail != null && emails.Contains(u.NormalizedEmail))
            .ToDictionaryAsync(u => u.NormalizedEmail!, u => u.Id);

        foreach (var row in result.Rows)
        {
            if (existingSerials.Contains(row.Device.SerialNumber, StringComparer.OrdinalIgnoreCase))
                result.Errors.Add($"Satır {row.Line}: seri numarası {row.Device.SerialNumber} zaten kayıtlı");
            if (row.AssignedEmail != null && !usersByEmail.ContainsKey(row.AssignedEmail.ToUpperInvariant()))
                result.Errors.Add($"Satır {row.Line}: {row.AssignedEmail} adresiyle kayıtlı kullanıcı yok");
        }

        if (result.Errors.Count > 0)
        {
            return View(result.Errors.OrderBy(e => e, StringComparer.Ordinal).ToList());
        }

        foreach (var row in result.Rows)
        {
            var device = row.Device;
            device.AssignedUserId = row.AssignedEmail != null ? usersByEmail[row.AssignedEmail.ToUpperInvariant()] : null;
            _context.Devices.Add(device);

            // Same records as a device added by hand
            if (device.AssignedUserId != null)
            {
                LogEvent(device, DeviceEventType.AssignmentChanged, $"Zimmetlendi: {await UserNameAsync(device.AssignedUserId)}");
                await UpdateAssignmentFormsAsync(device, device.AssignedUserId);
            }
        }

        await _context.SaveChangesAsync();
        TempData["DeviceMessage"] = $"{result.Rows.Count} cihaz içe aktarıldı.";
        return RedirectToAction(nameof(Index));
    }

    // GET: DEVICES/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }
        await LoadFormListsAsync(device);
        return View(device);
    }

    // POST: DEVICES/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Hostname,IpAddress,SerialNumber,Brand,Model,DeviceType,PurchaseDate,WarrantyEndDate,VendorName,VendorPhone,VendorEmail,Status,AssignedUserId,SwitchId,SwitchPort,VlanId")] Device device)
    {
        if (id != device.Id)
        {
            return NotFound();
        }

        var existing = await _context.Devices.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        await ValidateAssignedUserAsync(device.AssignedUserId);
        ValidateWarrantyDates(device);
        await ValidateSwitchPortAsync(device);

        if (ModelState.IsValid)
        {
            var newAssignedUserId = string.IsNullOrEmpty(device.AssignedUserId) ? null : device.AssignedUserId;
            var assignmentChanged = existing.AssignedUserId != newAssignedUserId;
            if (assignmentChanged)
            {
                LogEvent(existing, DeviceEventType.AssignmentChanged,
                    $"Zimmet: {await UserNameAsync(existing.AssignedUserId)} → {await UserNameAsync(newAssignedUserId)}");
            }
            if (existing.Status != device.Status)
            {
                LogEvent(existing, DeviceEventType.StatusChanged,
                    $"Durum: {existing.Status.GetDisplayName()} → {device.Status.GetDisplayName()}");
            }
            if (existing.SwitchId != device.SwitchId || existing.SwitchPort != device.SwitchPort)
            {
                LogEvent(existing, DeviceEventType.PortChanged,
                    $"Port: {await PortLabelAsync(existing.SwitchId, existing.SwitchPort)} → {await PortLabelAsync(device.SwitchId, device.SwitchPort)}");
            }

            // Only form fields are copied; CreatedAt stays as stored and LastUpdated is set by the server
            existing.Hostname = device.Hostname;
            existing.IpAddress = device.IpAddress;
            existing.SerialNumber = device.SerialNumber;
            existing.Brand = device.Brand;
            existing.Model = device.Model;
            existing.DeviceType = device.DeviceType;
            existing.PurchaseDate = device.PurchaseDate;
            existing.WarrantyEndDate = device.WarrantyEndDate;
            existing.VendorName = device.VendorName?.Trim();
            existing.VendorPhone = device.VendorPhone?.Trim();
            existing.VendorEmail = device.VendorEmail?.Trim();
            existing.Status = device.Status;
            existing.AssignedUserId = newAssignedUserId;
            existing.SwitchId = device.SwitchId;
            existing.SwitchPort = device.SwitchPort;
            existing.VlanId = device.VlanId;
            existing.LastUpdated = DateTime.UtcNow;

            // After the copy, so a new form records the device details as saved now
            if (assignmentChanged)
            {
                await UpdateAssignmentFormsAsync(existing, newAssignedUserId);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await LoadFormListsAsync(device);
        return View(device);
    }

    // GET: DEVICES/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var device = await _context.Devices
            .Include(d => d.AssignedUser)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (device == null)
        {
            return NotFound();
        }

        ViewData["LinkedTicketCount"] = await _context.Tickets.CountAsync(t => t.DeviceId == id);
        ViewData["ConnectedDeviceCount"] = await _context.Devices.CountAsync(d => d.SwitchId == id);
        return View(device);
    }

    // POST: DEVICES/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        // Tickets reference devices without cascade, so deleting a linked device would fail with an FK error.
        // Keeping the device also keeps its ticket history (and the dashboard's problem device report) intact.
        if (await _context.Tickets.AnyAsync(t => t.DeviceId == id))
        {
            return RedirectToAction(nameof(Delete), new { id });
        }

        var device = await _context.Devices.Include(d => d.ConnectedDevices).FirstOrDefaultAsync(d => d.Id == id);
        if (device != null)
        {
            // The switch FK has no SET NULL (self-reference), so unpatch connected devices first and keep a trace in their history
            foreach (var connected in device.ConnectedDevices)
            {
                LogEvent(connected, DeviceEventType.PortChanged,
                    $"Port: {device.Hostname} {connected.SwitchPort} → Bağlı değil (switch silindi)");
                connected.SwitchId = null;
                connected.SwitchPort = null;
                connected.VlanId = null;
                connected.LastUpdated = DateTime.UtcNow;
            }

            // Zimmet forms stay as records; an open one is closed since the device no longer exists
            await UpdateAssignmentFormsAsync(device, null);

            _context.Devices.Remove(device);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
