using HelpdeskOps.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Device stickers point here. Sign-in is required, so a QR code alone reveals nothing about the device.
[Authorize]
public class ScanController : Controller
{
    private readonly ApplicationDbContext _context;

    public ScanController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /d/5
    // A short path keeps the QR code small and easy to scan. IT staff land on the device card, everyone else on a new ticket for it.
    [HttpGet("/d/{id:int}")]
    public async Task<IActionResult> Device(int id)
    {
        if (!await _context.Devices.AnyAsync(d => d.Id == id))
        {
            return NotFound();
        }

        return User.IsInRole("ITAdmin")
            ? RedirectToAction("Details", "Devices", new { id })
            : RedirectToAction("Create", "Tickets", new { deviceId = id });
    }
}
