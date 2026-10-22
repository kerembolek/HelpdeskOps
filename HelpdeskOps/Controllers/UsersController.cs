using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// User list for IT staff
[Authorize(Roles = "ITAdmin")]
public class UsersController : Controller
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Users?q=&role=
    public async Task<IActionResult> Index(string? q, string? role)
    {
        var users = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            users = users.Where(u => (u.FirstName + " " + u.LastName).Contains(term)
                || (u.Email != null && u.Email.Contains(term))
                || (u.Department != null && u.Department.Contains(term)));
        }

        var rows = await users
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new
            {
                u.Id,
                Name = u.FirstName + " " + u.LastName,
                u.Email,
                u.Department,
                DeviceCount = u.AssignedDevices.Count,
                // Tickets assigned to this person that are still being worked on
                ActiveTicketCount = u.AssignedTickets.Count(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)
            })
            .ToListAsync();

        var rolesByUser = (await (
                from userRole in _context.UserRoles
                join r in _context.Roles on userRole.RoleId equals r.Id
                select new { userRole.UserId, r.Name })
            .ToListAsync())
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).MaxBy(n => Array.IndexOf(AppRoles.All, n)));

        var model = rows
            .Select(u => new UserListRow(u.Id, u.Name, u.Email, u.Department, rolesByUser.GetValueOrDefault(u.Id), u.DeviceCount, u.ActiveTicketCount))
            .Where(u => string.IsNullOrEmpty(role) || u.Role == role)
            .ToList();

        ViewBag.Query = q;
        ViewBag.Role = AppRoles.All.Contains(role) ? role : null;
        ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(q) || ViewBag.Role != null;
        return View(model);
    }
}
