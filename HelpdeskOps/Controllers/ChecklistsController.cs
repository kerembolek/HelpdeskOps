using HelpdeskOps;
using System.Security.Claims;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

// Onboarding and offboarding checklists: the manual steps IT ticks off, next to what HelpdeskOps can check itself
// (accounts, zimmetli devices, signed zimmet forms, and tickets)
[Authorize(Roles = "ITAdmin")]
public class ChecklistsController : Controller
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _context;
    private readonly StaffChecklistService _service;

    public ChecklistsController(ApplicationDbContext context, StaffChecklistService service)
    {
        _context = context;
        _service = service;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET: Checklists?status=open&type=Offboarding&page=2
    public async Task<IActionResult> Index(string? status, ChecklistType? type, int page = 1)
    {
        var checklists = _context.StaffChecklists.AsNoTracking();

        status = status is "done" or "all" ? status : "open";
        checklists = status switch
        {
            "open" => checklists.Where(c => c.CompletedAt == null).OrderBy(c => c.TargetDate).ThenBy(c => c.Id),
            "done" => checklists.Where(c => c.CompletedAt != null).OrderByDescending(c => c.CompletedAt),
            _ => checklists.OrderByDescending(c => c.CreatedAt)
        };
        if (type != null)
        {
            checklists = checklists.Where(c => c.Type == type);
        }

        var rows = checklists.Select(c => new StaffChecklistRow(c.Id, c.Type, c.User!.FirstName + " " + c.User.LastName, c.User.Department,
            c.TargetDate, c.CreatedAt, c.CompletedAt, c.Items.Count(i => i.DoneAt != null), c.Items.Count));

        ViewBag.Status = status;
        ViewBag.Type = type;
        return View(await PagedList<StaffChecklistRow>.CreateAsync(rows, page, PageSize));
    }

    // GET: Checklists/Create?userId=abc&type=Offboarding
    public async Task<IActionResult> Create(string? userId, ChecklistType type = ChecklistType.Onboarding)
    {
        await LoadUsersAsync(userId);
        return View(new StaffChecklistCreateViewModel { UserId = userId, Type = type, TargetDate = TurkeyTime.Today });
    }

    // POST: Checklists/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffChecklistCreateViewModel model)
    {
        // The person comes from a dropdown, but is checked here too
        var user = model.UserId == null ? null : await _context.Users.FirstOrDefaultAsync(u => u.Id == model.UserId && u.AnonymizedAt == null);
        if (model.UserId != null && user == null)
        {
            ModelState.AddModelError(nameof(model.UserId), "Geçerli bir kişi seçin.");
        }
        if (!Enum.IsDefined(model.Type))
        {
            ModelState.AddModelError(nameof(model.Type), "Geçersiz süreç.");
        }
        if (user != null && model.Type == ChecklistType.Offboarding && user.Id == CurrentUserId)
        {
            ModelState.AddModelError(nameof(model.UserId), "Kendiniz için işten çıkış listesi açamazsınız.");
        }
        if (user != null && await _context.StaffChecklists.AnyAsync(c => c.UserId == user.Id && c.Type == model.Type && c.CompletedAt == null))
        {
            ModelState.AddModelError(nameof(model.UserId), $"Bu kişinin açık bir {Lower(model.Type)} listesi zaten var.");
        }

        if (!ModelState.IsValid)
        {
            await LoadUsersAsync(model.UserId);
            return View(model);
        }

        var checklist = new StaffChecklist
        {
            Type = model.Type,
            UserId = user!.Id,
            TargetDate = model.TargetDate!.Value.Date,
            Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedById = CurrentUserId
        };
        var order = 0;
        foreach (var title in StaffChecklistRules.DefaultItems(model.Type))
        {
            checklist.Items.Add(new ChecklistItem { Title = title, SortOrder = order++ });
        }
        _context.StaffChecklists.Add(checklist);
        await _context.SaveChangesAsync();

        TempData["ChecklistMessage"] = $"{user.FirstName} {user.LastName} için {Lower(model.Type)} listesi açıldı.";
        return RedirectToAction(nameof(Details), new { id = checklist.Id });
    }

    // GET: Checklists/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var checklist = await _context.StaffChecklists.AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.CreatedBy)
            .Include(c => c.CompletedBy)
            .Include(c => c.Items).ThenInclude(i => i.DoneBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (checklist == null)
        {
            return NotFound();
        }
        checklist.Items = checklist.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();

        var checks = await _service.ChecksAsync(checklist);
        var devices = await _context.Devices.AsNoTracking()
            .Where(d => d.AssignedUserId == checklist.UserId)
            .OrderBy(d => d.Hostname)
            .ToListAsync();
        // Open forms, plus the ones closed while this checklist has been open
        var forms = await _context.AssignmentForms.AsNoTracking()
            .Where(f => f.UserId == checklist.UserId && (f.ReturnedAt == null || f.ReturnedAt >= checklist.CreatedAt))
            .OrderByDescending(f => f.IssuedAt)
            .ToListAsync();

        return View(new StaffChecklistDetailsViewModel
        {
            Checklist = checklist,
            Checks = checks,
            Devices = devices,
            Forms = forms,
            ActiveAssignedTickets = await _context.Tickets.CountAsync(t => t.AssignedToId == checklist.UserId
                && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)),
            CompletionBlocker = StaffChecklistRules.CompletionBlocker(checklist, checks)
        });
    }

    // POST: Checklists/ToggleItem/5?itemId=12
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleItem(int id, int itemId)
    {
        var item = await _context.ChecklistItems.Include(i => i.Checklist)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.StaffChecklistId == id);
        if (item == null)
        {
            return NotFound();
        }
        if (item.Checklist!.CompletedAt != null)
        {
            TempData["ChecklistError"] = "Tamamlanmış listenin adımları değiştirilemez.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var done = item.DoneAt == null;
        item.DoneAt = done ? DateTime.UtcNow : null;
        item.DoneById = done ? CurrentUserId : null;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: Checklists/AddItem/5
    // A company-specific step for this one person, such as a second monitor or access to a particular program
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(int id, string? title)
    {
        var checklist = await _context.StaffChecklists.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id);
        if (checklist == null)
        {
            return NotFound();
        }

        title = title?.Trim();
        if (checklist.CompletedAt != null)
        {
            TempData["ChecklistError"] = "Tamamlanmış listeye adım eklenemez.";
        }
        else if (string.IsNullOrEmpty(title) || title.Length > 200)
        {
            TempData["ChecklistError"] = "Adım 1 ile 200 karakter arasında olmalıdır.";
        }
        else
        {
            checklist.Items.Add(new ChecklistItem { Title = title, SortOrder = checklist.Items.Select(i => i.SortOrder).DefaultIfEmpty(-1).Max() + 1 });
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: Checklists/Complete/5
    // Checked again on the server; the page could be stale
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var checklist = await _context.StaffChecklists.Include(c => c.Items).Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
        if (checklist == null)
        {
            return NotFound();
        }

        var blocker = await _service.CompleteAsync(checklist, CurrentUserId);
        if (blocker != null)
        {
            TempData["ChecklistError"] = blocker;
        }
        else
        {
            var name = $"{checklist.User!.FirstName} {checklist.User.LastName}";
            TempData["ChecklistMessage"] = checklist.Type == ChecklistType.Offboarding
                ? $"İşten çıkış tamamlandı. {name} artık giriş yapamaz."
                : $"İşe giriş tamamlandı. {name} hazır.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: Checklists/Delete/5
    // For a list opened by mistake or a hire that did not happen; completed lists stay as a record
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var checklist = await _context.StaffChecklists.FirstOrDefaultAsync(c => c.Id == id);
        if (checklist == null)
        {
            return NotFound();
        }
        if (checklist.CompletedAt != null)
        {
            TempData["ChecklistError"] = "Tamamlanmış liste silinemez.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.StaffChecklists.Remove(checklist);
        await _context.SaveChangesAsync();
        TempData["ChecklistMessage"] = "Liste silindi.";
        return RedirectToAction(nameof(Index));
    }

    // "işe giriş" / "işten çıkış" in the middle of a sentence
    private static string Lower(ChecklistType type) => type == ChecklistType.Onboarding ? "işe giriş" : "işten çıkış";

    private async Task LoadUsersAsync(string? selectedId)
    {
        var users = await _context.Users.AsNoTracking()
            .Where(u => u.AnonymizedAt == null)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName + (u.Department != null ? " (" + u.Department + ")" : "") })
            .ToListAsync();
        ViewBag.Users = new SelectList(users, "Id", "Name", selectedId);
    }
}
