using System.Security.Claims;
using HelpdeskOps.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.ViewComponents;

// Navbar link to the notifications page with the number of unread notifications
public class NotificationBellViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public NotificationBellViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = userId == null ? 0 : await _context.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null);
        return View(unread);
    }
}
