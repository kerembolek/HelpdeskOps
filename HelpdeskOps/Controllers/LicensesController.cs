using System.Security.Claims;
using HelpdeskOps;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

// Software licenses: how many seats the company bought, who uses them, and when they renew
[Authorize(Roles = "ITAdmin")]
public class LicensesController : Controller
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _context;

    public LicensesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Licenses?renewal=expiring&userId=abc&page=2
    public async Task<IActionResult> Index(string? renewal, string? userId, int page = 1)
    {
        var licenses = _context.SoftwareLicenses.AsNoTracking();
        var today = TurkeyTime.Today;

        switch (renewal)
        {
            case "expiring":
                var soon = today.AddDays(Device.WarrantyExpiringSoonDays);
                licenses = licenses.Where(l => l.RenewalDate >= today && l.RenewalDate <= soon);
                break;
            case "expired":
                licenses = licenses.Where(l => l.RenewalDate < today);
                break;
            default:
                renewal = null;
                break;
        }

        // The licenses one person holds, for an offboarding
        string? userName = null;
        if (!string.IsNullOrEmpty(userId))
        {
            userName = await _context.Users.Where(u => u.Id == userId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync();
            licenses = licenses.Where(l => l.Assignments.Any(a => a.UserId == userId));
        }

        var rows = licenses
            .OrderBy(l => l.Name).ThenBy(l => l.Id)
            .Select(l => new LicenseListRow(l.Id, l.Name, l.Vendor, l.Seats, l.Assignments.Count, l.RenewalDate));

        ViewBag.Renewal = renewal;
        ViewBag.UserId = userName != null ? userId : null;
        ViewBag.UserName = userName;
        return View(await PagedList<LicenseListRow>.CreateAsync(rows, page, PageSize));
    }

    // GET: Licenses/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var license = await _context.SoftwareLicenses.AsNoTracking()
            .Include(l => l.Assignments).ThenInclude(a => a.User)
            .Include(l => l.Assignments).ThenInclude(a => a.AssignedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (license == null)
        {
            return NotFound();
        }

        var holders = license.Assignments.Select(a => a.UserId).ToList();
        var users = await _context.Users.AsNoTracking()
            .Where(u => u.AnonymizedAt == null && !holders.Contains(u.Id))
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName + (u.Department != null ? " (" + u.Department + ")" : "") })
            .ToListAsync();
        ViewBag.Users = new SelectList(users, "Id", "Name");
        license.Assignments = license.Assignments.OrderBy(a => a.User!.FirstName).ThenBy(a => a.User!.LastName).ToList();
        return View(license);
    }

    // GET: Licenses/Create
    public IActionResult Create() => View(new SoftwareLicense { Name = "" });

    // POST: Licenses/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Vendor,Seats,RenewalDate,Notes")] SoftwareLicense license)
    {
        if (!ModelState.IsValid)
        {
            return View(license);
        }

        license.CreatedAt = DateTime.UtcNow;
        _context.SoftwareLicenses.Add(license);
        await _context.SaveChangesAsync();
        TempData["LicenseMessage"] = $"{license.Name} eklendi.";
        return RedirectToAction(nameof(Details), new { id = license.Id });
    }

    // GET: Licenses/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var license = await _context.SoftwareLicenses.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        return license == null ? NotFound() : View(license);
    }

    // POST: Licenses/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Name,Vendor,Seats,RenewalDate,Notes")] SoftwareLicense form)
    {
        var license = await _context.SoftwareLicenses.Include(l => l.Assignments).FirstOrDefaultAsync(l => l.Id == id);
        if (license == null)
        {
            return NotFound();
        }

        // Fewer seats than people using it would hide a real shortage
        if (form.Seats < license.Assignments.Count)
        {
            ModelState.AddModelError(nameof(form.Seats), $"Bu lisansı {license.Assignments.Count} kişi kullanıyor; önce atamaları azaltın.");
        }
        if (!ModelState.IsValid)
        {
            form.Id = id;
            return View(form);
        }

        license.Name = form.Name;
        license.Vendor = form.Vendor;
        license.Seats = form.Seats;
        license.RenewalDate = form.RenewalDate?.Date;
        license.Notes = form.Notes;
        await _context.SaveChangesAsync();
        TempData["LicenseMessage"] = "Lisans güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: Licenses/Assign/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, string? userId)
    {
        var license = await _context.SoftwareLicenses.Include(l => l.Assignments).FirstOrDefaultAsync(l => l.Id == id);
        if (license == null)
        {
            return NotFound();
        }

        // The person comes from a dropdown, but is checked here too
        var user = userId == null ? null : await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.AnonymizedAt == null);
        if (user == null)
        {
            TempData["LicenseError"] = "Geçerli bir kişi seçin.";
        }
        else if (license.Assignments.Any(a => a.UserId == user.Id))
        {
            TempData["LicenseError"] = $"{user.FirstName} {user.LastName} bu lisansı zaten kullanıyor.";
        }
        else if (license.Assignments.Count >= license.Seats)
        {
            TempData["LicenseError"] = "Boş kullanıcı hakkı kalmadı. Önce birinin lisansını geri alın ya da kullanıcı sayısını artırın.";
        }
        else
        {
            license.Assignments.Add(new LicenseAssignment
            {
                UserId = user.Id,
                AssignedAt = DateTime.UtcNow,
                AssignedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
            });
            await _context.SaveChangesAsync();
            TempData["LicenseMessage"] = $"Lisans {user.FirstName} {user.LastName} kişisine atandı.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: Licenses/Unassign/5?assignmentId=12
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unassign(int id, int assignmentId, string? returnUrl)
    {
        var assignment = await _context.LicenseAssignments.Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.SoftwareLicenseId == id);
        if (assignment == null)
        {
            return NotFound();
        }

        _context.LicenseAssignments.Remove(assignment);
        await _context.SaveChangesAsync();
        TempData["LicenseMessage"] = $"{assignment.User!.FirstName} {assignment.User.LastName} kişisinin lisansı geri alındı.";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Details), new { id });
    }

    // POST: Licenses/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var license = await _context.SoftwareLicenses.FirstOrDefaultAsync(l => l.Id == id);
        if (license == null)
        {
            return NotFound();
        }

        _context.SoftwareLicenses.Remove(license);
        await _context.SaveChangesAsync();
        TempData["LicenseMessage"] = $"{license.Name} silindi.";
        return RedirectToAction(nameof(Index));
    }
}
