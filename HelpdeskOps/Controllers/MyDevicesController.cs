using System.Security.Claims;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// The devices assigned to the signed-in user and their zimmet forms
[Authorize]
public class MyDevicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MyDevicesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: MyDevices
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var devices = await _context.Devices
            .Where(d => d.AssignedUserId == userId)
            .OrderBy(d => d.Hostname)
            .ToListAsync();

        // Open forms first, newest first within each group
        ViewBag.AssignmentForms = await _context.AssignmentForms
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.ReturnedAt != null)
            .ThenByDescending(f => f.IssuedAt)
            .ToListAsync();

        return View(devices);
    }

    // GET: MyDevices/Accept/5
    public async Task<IActionResult> Accept(int id)
    {
        var form = await FindOwnFormAsync(id);
        if (form == null)
        {
            return NotFound();
        }
        if (form.AcceptedAt != null || form.ReturnedAt != null)
        {
            return RedirectToForms();
        }

        return View(new AcceptAssignmentViewModel { Form = form });
    }

    // POST: MyDevices/Accept/5
    // The employee signs to confirm they received the device; only their own open, unconfirmed forms can be signed
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, AcceptAssignmentViewModel model)
    {
        var form = await FindOwnFormAsync(id);
        if (form == null)
        {
            return NotFound();
        }
        if (form.AcceptedAt != null || form.ReturnedAt != null)
        {
            return RedirectToForms();
        }

        var signature = ReadSignature(model.Signature);
        if (!ModelState.IsValid || signature == null)
        {
            model.Form = form;
            return View(model);
        }

        form.AcceptedAt = DateTime.UtcNow;
        form.AcceptanceSignature = signature;
        await _context.SaveChangesAsync();

        TempData["FormMessage"] = $"{form.Hostname} için zimmet formunu imzaladınız.";
        return RedirectToForms();
    }

    // GET: MyDevices/Return/5
    public async Task<IActionResult> Return(int id)
    {
        var form = await FindOwnFormAsync(id);
        if (form == null)
        {
            return NotFound();
        }
        if (!CanRequestReturn(form))
        {
            return RedirectToForms();
        }

        return View(new ReturnRequestViewModel { Form = form });
    }

    // POST: MyDevices/Return/5
    // The employee asks to hand the device back; IT checks it and closes the form when they receive it
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(int id, ReturnRequestViewModel model)
    {
        var form = await FindOwnFormAsync(id);
        if (form == null)
        {
            return NotFound();
        }
        if (!CanRequestReturn(form))
        {
            return RedirectToForms();
        }

        if (model.Reason is ReturnReason reason && !Enum.IsDefined(reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "Geçerli bir iade sebebi seçin.");
        }
        var signature = ReadSignature(model.Signature);
        if (!ModelState.IsValid || signature == null)
        {
            model.Form = form;
            return View(model);
        }

        form.ReturnRequestedAt = DateTime.UtcNow;
        form.ReturnReason = model.Reason;
        form.ReturnNote = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        form.ReturnSignature = signature;

        foreach (var admin in await _userManager.GetUsersInRoleAsync("ITAdmin"))
        {
            _context.Notifications.Add(new Notification
            {
                UserId = admin.Id,
                Message = $"{form.EmployeeName}, {form.Hostname} cihazını iade etmek istiyor ({model.Reason!.Value.GetDisplayName()}).",
                Url = $"/AssignmentForms/CheckIn/{form.Id}",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        TempData["FormMessage"] = $"{form.Hostname} için iade talebiniz BT birimine iletildi. Cihazı BT'ye teslim ettiğinizde kontrol edilip teslim alınacak.";
        return RedirectToForms();
    }

    private Task<AssignmentForm?> FindOwnFormAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return _context.AssignmentForms.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
    }

    // Only a signed, still open form without an earlier request can be returned
    private static bool CanRequestReturn(AssignmentForm form) =>
        form.AcceptedAt != null && form.ReturnedAt == null && form.ReturnRequestedAt == null;

    private byte[]? ReadSignature(string? dataUrl)
    {
        var signature = SignatureImage.FromDataUrl(dataUrl);
        if (signature == null && ModelState.IsValid)
        {
            ModelState.AddModelError("Signature", "İmza okunamadı, lütfen tekrar imzalayın.");
        }
        return signature;
    }

    private IActionResult RedirectToForms() => Redirect(Url.Action(nameof(Index)) + "#assignment-forms");
}
