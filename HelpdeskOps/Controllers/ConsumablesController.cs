using System.Security.Claims;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

// Toner, cartridges, and other consumables: what is in stock, what to order, and which printer used what
[Authorize(Roles = "ITAdmin")]
public class ConsumablesController : Controller
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _context;

    public ConsumablesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Consumables?low=true&page=2
    public async Task<IActionResult> Index(bool low = false, int page = 1)
    {
        var consumables = _context.Consumables.AsNoTracking();
        if (low)
        {
            consumables = consumables.Where(c => c.Stock <= c.MinimumStock);
        }

        ViewBag.Low = low;
        ViewBag.LowCount = await _context.Consumables.CountAsync(c => c.Stock <= c.MinimumStock);
        return View(await PagedList<Consumable>.CreateAsync(consumables.OrderBy(c => c.Name).ThenBy(c => c.Id), page, PageSize));
    }

    // GET: Consumables/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var consumable = await _context.Consumables.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (consumable == null)
        {
            return NotFound();
        }

        // The latest 50 movements are enough to see how fast it is used
        ViewBag.Movements = await _context.ConsumableMovements.AsNoTracking()
            .Include(m => m.Device)
            .Include(m => m.CreatedBy)
            .Where(m => m.ConsumableId == id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .ToListAsync();
        await LoadPrintersAsync();
        return View(consumable);
    }

    // GET: Consumables/Create
    public IActionResult Create() => View(new Consumable { Name = "" });

    // POST: Consumables/Create
    // The opening stock is recorded as the first movement, so the history adds up to the stock
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Kind,CompatibleModels,MinimumStock,Location")] Consumable consumable, int initialStock = 0)
    {
        if (initialStock < 0 || initialStock > ConsumableStock.MaxQuantity)
        {
            ModelState.AddModelError("initialStock", $"Başlangıç stoğu 0 ile {ConsumableStock.MaxQuantity} arasında olmalıdır.");
        }
        if (!ModelState.IsValid)
        {
            ViewBag.InitialStock = initialStock;
            return View(consumable);
        }

        consumable.CreatedAt = DateTime.UtcNow;
        consumable.Stock = initialStock;
        if (initialStock > 0)
        {
            consumable.Movements.Add(new ConsumableMovement
            {
                Quantity = initialStock,
                Note = "Başlangıç stoğu",
                CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
            });
        }
        _context.Consumables.Add(consumable);
        await _context.SaveChangesAsync();
        TempData["ConsumableMessage"] = $"{consumable.Name} eklendi.";
        return RedirectToAction(nameof(Details), new { id = consumable.Id });
    }

    // GET: Consumables/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var consumable = await _context.Consumables.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return consumable == null ? NotFound() : View(consumable);
    }

    // POST: Consumables/Edit/5
    // Stock is not a form field; it changes only through movements
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Name,Kind,CompatibleModels,MinimumStock,Location")] Consumable form)
    {
        var consumable = await _context.Consumables.FirstOrDefaultAsync(c => c.Id == id);
        if (consumable == null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            form.Id = id;
            form.Stock = consumable.Stock;
            return View(form);
        }

        consumable.Name = form.Name;
        consumable.Kind = form.Kind;
        consumable.CompatibleModels = form.CompatibleModels;
        consumable.MinimumStock = form.MinimumStock;
        consumable.Location = form.Location;
        await _context.SaveChangesAsync();
        TempData["ConsumableMessage"] = "Sarf malzemesi güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: Consumables/Move/5
    // direction "in" adds stock (a delivery); "out" takes it, optionally into a printer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, string? direction, int quantity, int? deviceId, string? note)
    {
        var consumable = await _context.Consumables.FirstOrDefaultAsync(c => c.Id == id);
        if (consumable == null)
        {
            return NotFound();
        }

        var signed = direction == "out" ? -Math.Abs(quantity) : Math.Abs(quantity);
        var error = direction is "in" or "out" ? ConsumableStock.Check(consumable, signed) : "Geçersiz işlem.";

        // The printer comes from a dropdown, but is checked here too
        if (error == null && signed < 0 && deviceId != null && !await _context.Devices.AnyAsync(d => d.Id == deviceId))
        {
            error = "Seçilen cihaz bulunamadı.";
        }
        note = note?.Trim();
        if (error == null && note?.Length > 200)
        {
            error = "Not en fazla 200 karakter olabilir.";
        }

        if (error != null)
        {
            TempData["ConsumableError"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }

        consumable.Stock += signed;
        _context.ConsumableMovements.Add(new ConsumableMovement
        {
            ConsumableId = id,
            Quantity = signed,
            DeviceId = signed < 0 ? deviceId : null,
            Note = string.IsNullOrEmpty(note) ? null : note,
            CreatedAt = DateTime.UtcNow,
            CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ConsumableError"] = "Stok aynı anda başka biri tarafından değiştirildi. Tekrar deneyin.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["ConsumableMessage"] = signed > 0
            ? $"{signed} adet stoğa eklendi."
            : $"{-signed} adet stoktan düşüldü." + (consumable.IsLow ? " Stok en az seviyede; sipariş zamanı." : "");
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: Consumables/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var consumable = await _context.Consumables.FirstOrDefaultAsync(c => c.Id == id);
        if (consumable == null)
        {
            return NotFound();
        }

        _context.Consumables.Remove(consumable);
        await _context.SaveChangesAsync();
        TempData["ConsumableMessage"] = $"{consumable.Name} silindi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadPrintersAsync()
    {
        var printers = await _context.Devices.AsNoTracking()
            .Where(d => d.DeviceType == DeviceType.Printer && d.Status != DeviceStatus.Retired)
            .OrderBy(d => d.Hostname)
            .Select(d => new { d.Id, Name = d.Hostname + (d.Model != null ? " (" + d.Model + ")" : "") })
            .ToListAsync();
        ViewBag.Printers = new SelectList(printers, "Id", "Name");
    }
}
