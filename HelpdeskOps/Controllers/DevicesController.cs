
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpdeskOps.Models;
using HelpdeskOps.Data;

public class DevicesController : Controller
{
    private readonly ApplicationDbContext _context;

    public DevicesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: DEVICES
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Devices.ToListAsync());
    }

    // GET: DEVICES/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var device = await _context.Devices
            .FirstOrDefaultAsync(m => m.Id == id);
        if (device == null)
        {
            return NotFound();
        }

        return View(device);
    }

    // GET: DEVICES/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: DEVICES/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Hostname,IpAddress,SerialNumber,Brand,Model,DeviceType,WarrantyEndDate,Status")] Device device)
    {
        if (ModelState.IsValid)
        {
            // Timestamps are set by the server, not by the form
            device.CreatedAt = DateTime.UtcNow;
            device.LastUpdated = DateTime.UtcNow;

            _context.Add(device);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(device);
    }

    // GET: DEVICES/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }
        return View(device);
    }

    // POST: DEVICES/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Hostname,IpAddress,SerialNumber,Brand,Model,DeviceType,WarrantyEndDate,Status,CreatedAt,LastUpdated,AssignedUserId,AssignedUser")] Device device)
    {
        if (id != device.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(device);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DeviceExists(device.Id))
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
        return View(device);
    }

    // GET: DEVICES/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var device = await _context.Devices
            .FirstOrDefaultAsync(m => m.Id == id);
        if (device == null)
        {
            return NotFound();
        }

        return View(device);
    }

    // POST: DEVICES/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device != null)
        {
            _context.Devices.Remove(device);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool DeviceExists(int? id)
    {
        return _context.Devices.Any(e => e.Id == id);
    }
}
