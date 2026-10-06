
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpdeskOps.Models;
using HelpdeskOps.Data;

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
        return View(await _context.Tickets.ToListAsync());
    }

    // GET: TICKETS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var ticket = await _context.Tickets
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
        return View();
    }

    // POST: TICKETS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Title,Description,Category,Priority,Status,CreatedAt,UpdatedAt,ResolvedAt,DeviceId,Device,RequesterId,Requester,AssignedToId,AssignedTo")] Ticket ticket)
    {
        if (ModelState.IsValid)
        {
            _context.Add(ticket);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
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
        return View(ticket);
    }

    // POST: TICKETS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Title,Description,Category,Priority,Status,CreatedAt,UpdatedAt,ResolvedAt,DeviceId,Device,RequesterId,Requester,AssignedToId,AssignedTo")] Ticket ticket)
    {
        if (id != ticket.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(ticket);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TicketExists(ticket.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
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
