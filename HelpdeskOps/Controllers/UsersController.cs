using System.Security.Claims;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services.Kvkk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// User list and role management for IT staff
[Authorize(Roles = "ITAdmin")]
public class UsersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PersonalDataExporter _exporter;
    private readonly PersonalDataAnonymizer _anonymizer;

    public UsersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, PersonalDataExporter exporter,
        PersonalDataAnonymizer anonymizer)
    {
        _context = context;
        _userManager = userManager;
        _exporter = exporter;
        _anonymizer = anonymizer;
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
                u.TwoFactorEnabled,
                u.LockoutEnd,
                u.AnonymizedAt,
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
            .Select(u => new UserListRow(u.Id, u.Name, u.Email, u.Department, rolesByUser.GetValueOrDefault(u.Id), u.DeviceCount, u.ActiveTicketCount,
                u.TwoFactorEnabled, u.LockoutEnd > DateTimeOffset.UtcNow && u.AnonymizedAt == null, u.AnonymizedAt != null))
            .Where(u => string.IsNullOrEmpty(role) || u.Role == role)
            .ToList();

        ViewBag.Query = q;
        ViewBag.Role = AppRoles.All.Contains(role) ? role : null;
        ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(q) || ViewBag.Role != null;
        ViewBag.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return View(model);
    }

    // POST: Users/Unlock/abc
    // Lifts a lockout from too many wrong passwords, for someone who called the helpdesk
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id, string? returnUrl)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.AnonymizedAt != null)
        {
            return NotFound();
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        TempData["UserMessage"] = $"{user.FirstName} {user.LastName} hesabının kilidi açıldı.";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    // POST: Users/ChangeRole/abc
    // Gives the user exactly one role. Nobody can change their own role, so there is always at least one IT staff member left.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, string? role, string? returnUrl)
    {
        IActionResult Back() => Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (role == null || !AppRoles.All.Contains(role))
        {
            TempData["UserError"] = "Geçersiz rol.";
            return Back();
        }

        if (user.Id == currentUserId)
        {
            TempData["UserError"] = "Kendi rolünüzü değiştiremezsiniz.";
            return Back();
        }

        if (user.AnonymizedAt != null)
        {
            TempData["UserError"] = "Anonimleştirilmiş kullanıcıya rol verilemez.";
            return Back();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count == 1 && currentRoles[0] == role)
        {
            return Back();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var removed = await _userManager.RemoveFromRolesAsync(user, currentRoles.Intersect(AppRoles.All));
        var added = removed.Succeeded ? await _userManager.AddToRoleAsync(user, role) : removed;
        if (!added.Succeeded)
        {
            TempData["UserError"] = "Rol değiştirilemedi: " + string.Join(", ", added.Errors.Select(e => e.Description));
            return Back();
        }

        // Only IT staff can hold tickets, so a former IT staff member's active tickets go back to the unassigned pool
        var released = 0;
        if (currentRoles.Contains(AppRoles.ITAdmin) && role != AppRoles.ITAdmin)
        {
            var activeTickets = await _context.Tickets
                .Where(t => t.AssignedToId == user.Id && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress))
                .ToListAsync();
            foreach (var ticket in activeTickets)
            {
                ticket.AssignedToId = null;
                ticket.UpdatedAt = DateTime.UtcNow;
                _context.TicketActivities.Add(new TicketActivity
                {
                    Ticket = ticket,
                    ActorId = currentUserId,
                    Type = TicketActivityType.AssigneeChanged,
                    OldValue = $"{user.FirstName} {user.LastName}",
                    NewValue = "Atanmadı",
                    CreatedAt = DateTime.UtcNow
                });
            }
            released = activeTickets.Count;
        }

        // Invalidates the user's sign-in cookie so the old role stops working at the next check
        await _userManager.UpdateSecurityStampAsync(user);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["UserMessage"] = $"{user.FirstName} {user.LastName} artık {AppRoles.DisplayName(role)}."
            + (released > 0 ? $" Üzerindeki {released} aktif talep atanmamış taleplere alındı." : "");
        return Back();
    }

    // POST: Users/ExportData/abc
    // Everything kept about a person, for a KVKK request they made to the company; the audit log records the download
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportData(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var data = await _exporter.ExportAsync(user.Id);
        await _exporter.RecordExportAsync(user.Id, HttpContext);
        return File(data!, "application/json", PersonalDataExporter.FileName(user));
    }

    // GET: Users/Anonymize/abc
    // Confirmation page: what will be erased, what stays, and anything that has to be done first
    public async Task<IActionResult> Anonymize(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        ViewBag.Blocker = await _anonymizer.CheckAsync(user, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        ViewBag.TicketCount = await _context.Tickets.CountAsync(t => t.RequesterId == user.Id);
        ViewBag.FormCount = await _context.AssignmentForms.CountAsync(f => f.UserId == user.Id);
        return View(user);
    }

    // POST: Users/Anonymize/abc
    // KVKK deletion for a former employee: erases who they were but keeps the company records they are part of
    [HttpPost, ActionName("Anonymize")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnonymizeConfirmed(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Checked again on the server; the confirmation page could be stale
        var blocker = await _anonymizer.CheckAsync(user, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (blocker != null)
        {
            TempData["UserError"] = blocker;
            return RedirectToAction(nameof(Anonymize), new { id });
        }

        var name = $"{user.FirstName} {user.LastName}";
        await _anonymizer.AnonymizeAsync(user);

        TempData["UserMessage"] = $"{name} anonimleştirildi. Kayıtlarında artık \"{PersonalDataAnonymizer.AnonymousName}\" görünüyor.";
        return RedirectToAction(nameof(Index));
    }
}
