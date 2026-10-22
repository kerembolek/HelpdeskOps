using System.Security.Claims;
using HelpdeskOps.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// The devices assigned to the signed-in user and their zimmet forms
[Authorize]
public class MyDevicesController : Controller
{
    private readonly ApplicationDbContext _context;

    public MyDevicesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: MyDevices
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var devices = await _context.Devices
            .Where(d => d.AssignedUserId == userId)
            .OrderBy(d => d.Hostname)
            .ToListAsync();

        // Open forms first, newest first within each group
        ViewBag.AssignmentForms = await _context.AssignmentForms
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.ReturnedAt != null)
            .ThenByDescending(f => f.IssuedAt)
            .ToListAsync();

        return View(devices);
    }

    // POST: MyDevices/Accept/5
    // The employee confirms they received the device; only their own open, unconfirmed forms can be confirmed
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var form = await _context.AssignmentForms.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
        if (form == null)
        {
            return NotFound();
        }

        if (form.AcceptedAt == null && form.ReturnedAt == null)
        {
            form.AcceptedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Redirect(Url.Action(nameof(Index)) + "#assignment-forms");
    }
}
