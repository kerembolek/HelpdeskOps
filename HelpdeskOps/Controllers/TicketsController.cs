
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
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
    private readonly AttachmentStorage _storage;

    public TicketsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, AttachmentStorage storage)
    {
        _context = context;
        _userManager = userManager;
        _storage = storage;
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

    // The subcategory must belong to the chosen category; "Other" needs a short description, which is dropped otherwise
    private void ValidateSubcategory(Ticket ticket)
    {
        ticket.OtherCategoryText = string.IsNullOrWhiteSpace(ticket.OtherCategoryText) ? null : ticket.OtherCategoryText.Trim();

        if (ticket.Subcategory is not TicketSubcategory subcategory)
        {
            ModelState.AddModelError(nameof(Ticket.Subcategory), "Alt kategori seçilmelidir.");
            return;
        }

        if (!TicketCategories.Belongs(ticket.Category, subcategory))
        {
            ModelState.AddModelError(nameof(Ticket.Subcategory), "Seçilen alt kategori bu kategoriye ait değil.");
        }

        if (subcategory != TicketSubcategory.Other)
        {
            ticket.OtherCategoryText = null;
        }
        else if (ticket.OtherCategoryText == null)
        {
            ModelState.AddModelError(nameof(Ticket.OtherCategoryText), "\"Diğer\" seçtiğinizde konuyu kısaca yazın.");
        }
    }

    // Queues an in-app notification about the ticket; nobody is notified about their own action
    private void Notify(string? userId, Ticket ticket, string message)
    {
        if (userId == null || userId == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            return;
        }

        var title = ticket.Title.Length > 80 ? ticket.Title[..80] + "…" : ticket.Title;
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Message = $"#{ticket.Id} \"{title}\": {message}",
            Url = $"/Tickets/Details/{ticket.Id}",
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
            .Where(u => u.AnonymizedAt == null)
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

    // ITAdmin works on every ticket; Manager can read every ticket but not change it
    private bool CanSeeAllTickets() => User.IsInRole("ITAdmin") || User.IsInRole("Manager");

    // GET: TICKETS/Index?q=&category=&status=&priority=&scope=
    // scope: "active" (open + in progress), "done" (resolved + closed), "overdue" (active and past the SLA target),
    // "mine" (ITAdmin only) and "unassigned" (ITAdmin and Manager; active and without an assignee)
    public async Task<IActionResult> Index(string? q, TicketCategory? category, TicketStatus? status, TicketPriority? priority, string? scope)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("ITAdmin");
        var canSeeAll = CanSeeAllTickets();

        var query = _context.Tickets
            .Include(t => t.Device)
            .Include(t => t.Requester)
            .Include(t => t.AssignedTo)
            .AsQueryable();

        if (!canSeeAll)
        {
            query = query.Where(t => t.RequesterId == userId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t => t.Title.Contains(term) || t.Description.Contains(term)
                || (t.OtherCategoryText != null && t.OtherCategoryText.Contains(term)));
        }

        if (category != null)
        {
            query = query.Where(t => t.Category == category);
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
            case "overdue":
                query = query.Where(TicketSla.IsOverdue(DateTime.UtcNow));
                break;
            case "mine" when isAdmin:
                query = query.Where(t => t.AssignedToId == userId);
                break;
            case "unassigned" when canSeeAll:
                query = query.Where(t => t.AssignedToId == null
                    && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress));
                break;
            default:
                scope = null;
                break;
        }

        ViewBag.Query = q;
        ViewBag.Category = category;
        ViewBag.Status = status;
        ViewBag.Priority = priority;
        ViewBag.Scope = scope;
        ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(q) || category != null || status != null || priority != null || scope != null;

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
            .Include(t => t.Attachments.OrderBy(a => a.UploadedAt)).ThenInclude(a => a.UploadedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id);
        if (ticket == null)
        {
            return NotFound();
        }

        if (!CanSeeAllTickets() && ticket.RequesterId != User.FindFirstValue(ClaimTypes.NameIdentifier))
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

            // The other side of the conversation hears about it
            if (userId == ticket.RequesterId)
            {
                Notify(ticket.AssignedToId, ticket, "talep eden yeni bir yorum yazdı.");
            }
            else
            {
                Notify(ticket.RequesterId, ticket, "BT ekibi yeni bir yorum yazdı.");
            }
            await _context.SaveChangesAsync();
        }

        return Redirect(Url.Action(nameof(Details), new { id }) + "#comments");
    }

    // GET: TICKETS/Create
    // category and solutionId come from the Tier 0 solution bank ("Çözülmedi, talep aç")
    public async Task<IActionResult> Create(TicketCategory? category, int? solutionId)
    {
        await LoadDevicesAsync();
        if (User.IsInRole("ITAdmin"))
        {
            await LoadRequestersAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        var triedSolution = solutionId == null ? null
            : await _context.SolutionArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == solutionId && a.IsPublished);
        if (triedSolution == null && category == null)
        {
            return View();
        }

        ViewBag.TriedSolution = triedSolution?.Title;
        return View(new Ticket
        {
            Title = triedSolution?.Title ?? "",
            Description = triedSolution != null
                ? $"Kendin Çöz sayfasındaki \"{triedSolution.Title}\" adımlarını denedim, sorun devam ediyor.\n\n"
                : "",
            Category = triedSolution?.Category ?? category!.Value,
            Priority = TicketPriority.Medium,
            RequesterId = ""
        });
    }

    // POST: TICKETS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    // requesterId is only honored for ITAdmin, who can open tickets on behalf of a caller.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(AttachmentStorage.MaxFileSize * AttachmentStorage.MaxFilesPerUpload + 1024 * 1024)]
    public async Task<IActionResult> Create([Bind("Title,Description,Category,Subcategory,OtherCategoryText,Priority,DeviceId")] Ticket ticket, string? requesterId, List<IFormFile>? files)
    {
        files ??= new List<IFormFile>();
        if (AttachmentStorage.ValidateAll(files) is string fileError)
        {
            ModelState.AddModelError("files", fileError);
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isAdmin = User.IsInRole("ITAdmin");

        ticket.RequesterId = isAdmin && !string.IsNullOrEmpty(requesterId) ? requesterId : currentUserId;
        ticket.Status = TicketStatus.Open;
        ticket.CreatedAt = DateTime.UtcNow;

        ModelState.Remove(nameof(Ticket.RequesterId));
        ModelState.Remove(nameof(Ticket.Requester));

        // The requester must exist and must not be an anonymized former employee
        if (ticket.RequesterId != currentUserId && await _userManager.FindByIdAsync(ticket.RequesterId) is not { AnonymizedAt: null })
        {
            ModelState.AddModelError(nameof(Ticket.RequesterId), "Seçilen kullanıcı bulunamadı.");
        }

        if (ticket.DeviceId != null && !await DevicesForCurrentUser().AnyAsync(d => d.Id == ticket.DeviceId))
        {
            ModelState.AddModelError(nameof(Ticket.DeviceId), "Yalnızca size zimmetli bir cihaz seçebilirsiniz.");
        }

        ValidateSubcategory(ticket);

        if (ModelState.IsValid)
        {
            foreach (var file in files)
            {
                ticket.Attachments.Add(await _storage.SaveAsync(file, currentUserId));
            }

            _context.Add(ticket);
            LogActivity(ticket, TicketActivityType.Created, null, null);
            await _context.SaveChangesAsync();

            // A ticket IT opened on someone's behalf; saved first so the notification can link to its id
            if (ticket.RequesterId != currentUserId)
            {
                Notify(ticket.RequesterId, ticket, "adınıza BT ekibi tarafından talep açıldı.");
                await _context.SaveChangesAsync();
            }

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
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Title,Description,Category,Subcategory,OtherCategoryText,Priority,Status,DeviceId,AssignedToId")] Ticket ticket)
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

        ValidateSubcategory(ticket);

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
                Notify(ticket.AssignedToId, existing, "talep size atandı.");
            }
            if (existing.Status != ticket.Status)
            {
                Notify(existing.RequesterId, existing, $"talebinizin durumu \"{ticket.Status.GetDisplayName()}\" oldu.");
            }

            existing.Title = ticket.Title;
            existing.Description = ticket.Description;
            existing.Category = ticket.Category;
            existing.Subcategory = ticket.Subcategory;
            existing.OtherCategoryText = ticket.OtherCategoryText;
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

    // POST: TICKETS/Rate/5
    // Only the requester rates, only once, and only after the ticket is resolved or closed
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rate(int id, int? rating, string? comment)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null)
        {
            return NotFound();
        }

        if (ticket.RequesterId != User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            return Forbid();
        }

        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed) || ticket.SatisfactionRating != null)
        {
            TempData["RatingError"] = "Bu talep şu anda değerlendirilemez.";
        }
        else if (rating is not (>= 1 and <= 5))
        {
            TempData["RatingError"] = "Lütfen 1 ile 5 arasında bir puan seçin.";
        }
        else if (comment?.Length > 500)
        {
            TempData["RatingError"] = "Yorum en fazla 500 karakter olabilir.";
        }
        else
        {
            ticket.SatisfactionRating = rating;
            ticket.SatisfactionComment = comment;
            ticket.RatedAt = DateTime.UtcNow;
            LogActivity(ticket, TicketActivityType.Rated, null, $"{rating}/5");
            Notify(ticket.AssignedToId, ticket, $"talep eden çözümü {rating}/5 olarak değerlendirdi.");
            await _context.SaveChangesAsync();
        }

        return Redirect(Url.Action(nameof(Details), new { id }) + "#rating");
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
            Notify(ticket.RequesterId, ticket, "talebiniz BT ekibi tarafından üstlenildi ve işleme alındı.");
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
        var ticket = await _context.Tickets.Include(t => t.Attachments).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket != null)
        {
            _context.Tickets.Remove(ticket);
        }

        await _context.SaveChangesAsync();

        // The rows went with the ticket; the files on disk are removed once the delete has been saved
        foreach (var attachment in ticket?.Attachments ?? [])
        {
            _storage.Delete(attachment);
        }

        return RedirectToAction(nameof(Index));
    }
}
