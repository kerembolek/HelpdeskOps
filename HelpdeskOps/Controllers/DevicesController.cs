
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HelpdeskOps.Models;
using HelpdeskOps.Data;
using Microsoft.AspNetCore.Authorization;

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
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new { u.Id, FullName = u.FirstName + " " + u.LastName + (u.Department != null ? " (" + u.Department + ")" : "") })
            .ToListAsync();
        ViewBag.Users = new SelectList(users, "Id", "FullName", selectedId);
    }

    // Zimmet için seçilen kullanıcının gerçekten var olduğunu doğrular
    private async Task ValidateAssignedUserAsync(string? userId)
    {
        if (!string.IsNullOrEmpty(userId) && !await _context.Users.AnyAsync(u => u.Id == userId))
        {
            ModelState.AddModelError(nameof(Device.AssignedUserId), "Seçilen kullanıcı bulunamadı.");
        }
    }

    // GET: DEVICES
    public async Task<IActionResult> Index()
    {
        return View(await _context.Devices
            .Include(d => d.AssignedUser)
            .ToListAsync());
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
            .FirstOrDefaultAsync(m => m.Id == id);
        if (device == null)
        {
            return NotFound();
        }

        return View(device);
    }

    // GET: DEVICES/Create
    public async Task<IActionResult> Create()
    {
        await LoadUsersAsync();
        return View();
    }

    // POST: DEVICES/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Hostname,IpAddress,SerialNumber,Brand,Model,DeviceType,WarrantyEndDate,Status,AssignedUserId")] Device device)
    {
        await ValidateAssignedUserAsync(device.AssignedUserId);

        if (ModelState.IsValid)
        {
            // Timestamps are set by the server, not by the form
            device.CreatedAt = DateTime.UtcNow;
            device.LastUpdated = DateTime.UtcNow;

            _context.Add(device);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await LoadUsersAsync(device.AssignedUserId);
        return View(device);
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
        await LoadUsersAsync(device.AssignedUserId);
        return View(device);
    }

    // POST: DEVICES/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Hostname,IpAddress,SerialNumber,Brand,Model,DeviceType,WarrantyEndDate,Status,AssignedUserId")] Device device)
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

        if (ModelState.IsValid)
        {
            // Only form fields are copied; CreatedAt stays as stored and LastUpdated is set by the server
            existing.Hostname = device.Hostname;
            existing.IpAddress = device.IpAddress;
            existing.SerialNumber = device.SerialNumber;
            existing.Brand = device.Brand;
            existing.Model = device.Model;
            existing.DeviceType = device.DeviceType;
            existing.WarrantyEndDate = device.WarrantyEndDate;
            existing.Status = device.Status;
            existing.AssignedUserId = string.IsNullOrEmpty(device.AssignedUserId) ? null : device.AssignedUserId;
            existing.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await LoadUsersAsync(device.AssignedUserId);
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

        var device = await _context.Devices.FindAsync(id);
        if (device != null)
        {
            _context.Devices.Remove(device);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool DeviceExists(int? id)
    {
        return _context.Devices.Any(e => e.Id == id);
    }
}
