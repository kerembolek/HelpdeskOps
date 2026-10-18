using System.Security.Claims;
using HelpdeskOps.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Read-only list of the devices assigned to the signed-in user
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

        return View(devices);
    }
}
