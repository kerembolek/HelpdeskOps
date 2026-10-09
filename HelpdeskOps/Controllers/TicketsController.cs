
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class TicketsController : Controller
{
    private readonly ApplicationDbContext _context;

    public TicketsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: TICKETS
    public async Task<IActionResult> Index()
    {
        var tickets = await _context.Tickets
            .Include(t => t.Device)
            .Include(t => t.Requester)
            .Include(t => t.AssignedTo)
            .ToListAsync();
        return View(tickets);
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
        ticket.RequesterId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "dev-user-1";
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
        return View(ticket);
    }

    // POST: TICKETS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Title,Description,Category,Priority,Status,DeviceId")] Ticket ticket)
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
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        ViewData["DeviceId"] = new SelectList(_context.Devices, "Id", "Hostname", ticket.DeviceId);
        return View(ticket);
    }

    // GET: TICKETS/Delete/5
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
