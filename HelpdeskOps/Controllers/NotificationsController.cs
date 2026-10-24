using System.Security.Claims;
using HelpdeskOps.Data;
using HelpdeskOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// The signed-in user's own notifications
[Authorize]
public class NotificationsController : Controller
{
    private const int ListLimit = 50;

    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public NotificationsController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // GET: Notifications
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(ListLimit)
            .ToListAsync();

        return View(notifications);
    }

    // GET: Notifications/Open/5
    // Marks the notification as read and goes to the page it is about
    public async Task<IActionResult> Open(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification == null)
        {
            return NotFound();
        }

        // The read-only demo leaves notifications unread
        if (notification.ReadAt == null && !DemoReadOnlyMiddleware.IsEnabled(_configuration))
        {
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Url.IsLocalUrl(notification.Url) ? LocalRedirect(notification.Url) : RedirectToAction(nameof(Index));
    }

    // POST: Notifications/MarkAllRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _context.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));

        return RedirectToAction(nameof(Index));
    }
}
