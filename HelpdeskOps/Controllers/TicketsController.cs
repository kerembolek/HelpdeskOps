
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

[Authorize]
public class TicketsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    

    public TicketsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
    private async Task LoadAssigneesAsync(string? selectedId = null)
    {
        var admins = await _userManager.GetUsersInRoleAsync("ITAdmin");
        ViewBag.Assignees = new SelectList(
            admins.Select(u => new { u.Id, FullName = u.FirstName + " " + u.LastName }),
            "Id", "FullName", selectedId);
    }

    // Adds a history entry when a value actually changed
    private void LogActivity(Ticket ticket, TicketActivityType type, string? oldValue, string? newValue)
    {
        if (type != TicketActivityType.Created && oldValue == newValue)
        {
            return;
        }

        _context.TicketActivities.Add(new TicketActivity
        {
            Ticket = ticket,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Type = type,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<string> AssigneeNameAsync(string? userId)
    {
        if (userId == null)
        {
            return "Atanmadı";
        }

        var user = await _userManager.FindByIdAsync(userId);
        return user != null ? $"{user.FirstName} {user.LastName}" : "-";
    }

    private async Task LoadRequestersAsync(string? selectedId = null)
    {
        var users = await _context.Users
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new
            {
                u.Id,
                FullName = u.FirstName + " " + u.LastName + (u.Department != null ? " (" + u.Department + ")" : "")
            })
            .ToListAsync();
        ViewBag.Requesters = new SelectList(users, "Id", "FullName", selectedId);
    }

    // ITAdmin sees every device; other users only the devices assigned to them
    private IQueryable<Device> DevicesForCurrentUser()
    {
        if (User.IsInRole("ITAdmin"))
        {
            return _context.Devices;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return _context.Devices.Where(d => d.AssignedUserId == userId);
    }

    private async Task LoadDevicesAsync(int? selectedId = null)
    {
        var devices = await DevicesForCurrentUser().OrderBy(d => d.Hostname).ToListAsync();
        ViewData["DeviceId"] = new SelectList(devices, "Id", "Hostname", selectedId);
    }

    // GET: TICKETS/Index?q=&status=&priority=&scope=
    // scope: "active" (open + in progress), "done" (resolved + closed),
    // "mine" and "unassigned" (ITAdmin only; unassigned means active and without an assignee)
    public async Task<IActionResult> Index(string? q, TicketStatus? status, TicketPriority? priority, string? scope)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("ITAdmin");

        var query = _context.Tickets
            .Include(t => t.Device)
            .Include(t => t.Requester)
            .Include(t => t.AssignedTo)
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(t => t.RequesterId == userId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t => t.Title.Contains(term) || t.Description.Contains(term));
        }

        if (status != null)
        {
            query = query.Where(t => t.Status == status);
        }

        if (priority != null)
        {
            query = query.Where(t => t.Priority == priority);
        }

        switch (scope)
        {
            case "active":
                query = query.Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress);
                break;
            case "done":
                query = query.Where(t => t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed);
                break;
            case "mine" when isAdmin:
                query = query.Where(t => t.AssignedToId == userId);
                break;
            case "unassigned" when isAdmin:
                query = query.Where(t => t.AssignedToId == null
                    && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress));
                break;
            default:
                scope = null;
                break;
        }

        ViewBag.Query = q;
        ViewBag.Status = status;
        ViewBag.Priority = priority;
        ViewBag.Scope = scope;
        ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(q) || status != null || priority != null || scope != null;

        return View(await query.OrderByDescending(t => t.CreatedAt).ToListAsync());
    }

    // GET: TICKETS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var ticket = await _context.Tickets
            .Include(t => t.Device)
            .Include(t => t.Requester)
            .Include(t => t.AssignedTo)
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt)).ThenInclude(c => c.Author)
            .Include(t => t.Activities.OrderBy(a => a.CreatedAt)).ThenInclude(a => a.Actor)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id);
        if (ticket == null)
        {
            return NotFound();
        }

        if (!User.IsInRole("ITAdmin") && ticket.RequesterId != User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            return Forbid();
        }

        return View(ticket);
    }

    // POST: TICKETS/AddComment/5
    // The requester and IT staff can write on a ticket
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, string? body)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!User.IsInRole("ITAdmin") && ticket.RequesterId != userId)
        {
            return Forbid();
        }

        body = body?.Trim();
        if (string.IsNullOrEmpty(body))
        {
            TempData["CommentError"] = "Yorum boş olamaz.";
        }
        else if (body.Length > 2000)
        {
            TempData["CommentError"] = "Yorum en fazla 2000 karakter olabilir.";
            TempData["CommentDraft"] = body;
        }
        else
        {
            _context.TicketComments.Add(new TicketComment
            {
                TicketId = id,
                AuthorId = userId,
                Body = body,
                CreatedAt = DateTime.UtcNow
            });
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Redirect(Url.Action(nameof(Details), new { id }) + "#comments");
    }

    // GET: TICKETS/Create
    public async Task<IActionResult> Create()
    {
        await LoadDevicesAsync();
        if (User.IsInRole("ITAdmin"))
        {
            await LoadRequestersAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));
        }
        return View();
    }

    // POST: TICKETS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    // requesterId is only honored for ITAdmin, who can open tickets on behalf of a caller.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,Description,Category,Priority,DeviceId")] Ticket ticket, string? requesterId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isAdmin = User.IsInRole("ITAdmin");

        ticket.RequesterId = isAdmin && !string.IsNullOrEmpty(requesterId) ? requesterId : currentUserId;
        ticket.Status = TicketStatus.Open;
        ticket.CreatedAt = DateTime.UtcNow;

        ModelState.Remove(nameof(Ticket.RequesterId));
        ModelState.Remove(nameof(Ticket.Requester));

        if (ticket.RequesterId != currentUserId && await _userManager.FindByIdAsync(ticket.RequesterId) == null)
        {
            ModelState.AddModelError(nameof(Ticket.RequesterId), "Seçilen kullanıcı bulunamadı.");
        }

        if (ticket.DeviceId != null && !await DevicesForCurrentUser().AnyAsync(d => d.Id == ticket.DeviceId))
        {
            ModelState.AddModelError(nameof(Ticket.DeviceId), "Yalnızca size zimmetli bir cihaz seçebilirsiniz.");
        }

        if (ModelState.IsValid)
        {
            _context.Add(ticket);
            LogActivity(ticket, TicketActivityType.Created, null, null);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await LoadDevicesAsync(ticket.DeviceId);
        if (isAdmin)
        {
            await LoadRequestersAsync(ticket.RequesterId);
        }
        return View(ticket);
    }

    // GET: TICKETS/Edit/5
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null)
        {
            return NotFound();
        }
        ViewData["DeviceId"] = new SelectList(_context.Devices, "Id", "Hostname", ticket.DeviceId);
        await LoadAssigneesAsync(ticket.AssignedToId);
        return View(ticket);
    }

    // POST: TICKETS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Title,Description,Category,Priority,Status,DeviceId,AssignedToId")] Ticket ticket)
    {
        if (id != ticket.Id)
        {
            return NotFound();
        }

        var existing = await _context.Tickets.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        ModelState.Remove(nameof(Ticket.RequesterId));
        ModelState.Remove(nameof(Ticket.Requester));

        ticket.AssignedToId = string.IsNullOrEmpty(ticket.AssignedToId) ? null : ticket.AssignedToId;

        // The dropdowns only offer valid choices, but a crafted POST could send any id
        if (ticket.DeviceId != null && !await _context.Devices.AnyAsync(d => d.Id == ticket.DeviceId))
        {
            ModelState.AddModelError(nameof(Ticket.DeviceId), "Seçilen cihaz bulunamadı.");
        }

        if (ticket.AssignedToId != null)
        {
            var assignee = await _userManager.FindByIdAsync(ticket.AssignedToId);
            if (assignee == null || !await _userManager.IsInRoleAsync(assignee, "ITAdmin"))
            {
                ModelState.AddModelError(nameof(Ticket.AssignedToId), "Talepler yalnızca BT personeline atanabilir.");
            }
        }

        if (ModelState.IsValid)
        {
            LogActivity(existing, TicketActivityType.StatusChanged,
                existing.Status.GetDisplayName(), ticket.Status.GetDisplayName());
            LogActivity(existing, TicketActivityType.PriorityChanged,
                existing.Priority.GetDisplayName(), ticket.Priority.GetDisplayName());
            if (existing.AssignedToId != ticket.AssignedToId)
            {
                LogActivity(existing, TicketActivityType.AssigneeChanged,
                    await AssigneeNameAsync(existing.AssignedToId), await AssigneeNameAsync(ticket.AssignedToId));
            }

            existing.Title = ticket.Title;
            existing.Description = ticket.Description;
            existing.Category = ticket.Category;
            existing.Priority = ticket.Priority;
            existing.Status = ticket.Status;
            existing.DeviceId = ticket.DeviceId;
            existing.AssignedToId = ticket.AssignedToId;

            if (existing.Status is TicketStatus.Resolved or TicketStatus.Closed)
            {
                existing.ResolvedAt ??= DateTime.UtcNow;
            }
            else
            {
                existing.ResolvedAt = null;
            }

            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        ViewData["DeviceId"] = new SelectList(_context.Devices, "Id", "Hostname", ticket.DeviceId);
        await LoadAssigneesAsync(ticket.AssignedToId);
        return View(ticket);
    }

    // POST: TICKETS/AssignToMe/5
    // One-click self-assignment; an open ticket moves to "in progress" when someone takes it
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> AssignToMe(int id, string? returnUrl)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ticket.AssignedToId != userId)
        {
            LogActivity(ticket, TicketActivityType.AssigneeChanged,
                await AssigneeNameAsync(ticket.AssignedToId), await AssigneeNameAsync(userId));
        }
        ticket.AssignedToId = userId;
        if (ticket.Status == TicketStatus.Open)
        {
            LogActivity(ticket, TicketActivityType.StatusChanged,
                TicketStatus.Open.GetDisplayName(), TicketStatus.InProgress.GetDisplayName());
            ticket.Status = TicketStatus.InProgress;
        }
        ticket.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: TICKETS/Delete/5
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var ticket = await _context.Tickets
     .Include(t => t.Device)
     .Include(t => t.Requester)
     .Include(t => t.AssignedTo)
     .FirstOrDefaultAsync(m => m.Id == id);
        if (ticket == null)
        {
            return NotFound();
        }

        return View(ticket);
    }

    // POST: TICKETS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket != null)
        {
            _context.Tickets.Remove(ticket);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
