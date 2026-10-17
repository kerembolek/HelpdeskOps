
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

    // GET: TICKETS/Index
    public async Task<IActionResult> Index()
    {
        var query = _context.Tickets
            .Include(t => t.Device)
            .Include(t => t.Requester)
            .Include(t => t.AssignedTo)
            .AsQueryable();

        if (!User.IsInRole("ITAdmin"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(t => t.RequesterId == userId);
        }

        return View(await query.ToListAsync());
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

    // GET: TICKETS/Create
    public IActionResult Create()
    {
        ViewData["DeviceId"] = new SelectList(_context.Devices, "Id", "Hostname");
        return View();
    }

    // POST: TICKETS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,Description,Category,Priority,DeviceId")] Ticket ticket)
    {
        ticket.RequesterId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        ticket.Status = TicketStatus.Open;
        ticket.CreatedAt = DateTime.UtcNow;

        ModelState.Remove(nameof(Ticket.RequesterId));
        ModelState.Remove(nameof(Ticket.Requester));

        if (ModelState.IsValid)
        {
            _context.Add(ticket);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewData["DeviceId"] = new SelectList(_context.Devices, "Id", "Hostname");
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

        if (ModelState.IsValid)
        {
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

    private bool TicketExists(int? id)
    {
        return _context.Tickets.Any(e => e.Id == id);
    }
}
