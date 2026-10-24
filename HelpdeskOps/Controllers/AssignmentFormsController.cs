using System.Security.Claims;
using HelpdeskOps.Data;
using HelpdeskOps.Documents;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

// PDF copies of zimmet forms and IT check-in of returned devices
[Authorize]
public class AssignmentFormsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AssignmentFormsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: AssignmentForms/Pdf/5
    // IT staff can download any form; everyone else only the forms issued to them
    public async Task<IActionResult> Pdf(int id)
    {
        var form = await _context.AssignmentForms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (form == null)
        {
            return NotFound();
        }

        if (!User.IsInRole("ITAdmin") && form.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            return Forbid();
        }

        var pdf = new AssignmentFormDocument(form).GeneratePdf();
        return File(pdf, "application/pdf", $"{form.FormNumber}.pdf");
    }

    // Statuses a checked-in device can go to; it no longer belongs to anyone
    private static readonly DeviceStatus[] CheckInStatuses = { DeviceStatus.InStorage, DeviceStatus.Faulty, DeviceStatus.Retired };

    // GET: AssignmentForms/CheckIn/5
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> CheckIn(int id)
    {
        var form = await _context.AssignmentForms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (form == null)
        {
            return NotFound();
        }
        if (form.ReturnedAt != null)
        {
            return RedirectToDevice(form);
        }

        return View(new CheckInViewModel
        {
            Form = form,
            NewStatus = form.ReturnReason == ReturnReason.Faulty ? DeviceStatus.Faulty : DeviceStatus.InStorage
        });
    }

    // POST: AssignmentForms/CheckIn/5
    // IT receives the device, records its condition, and signs; the form closes and the device leaves the employee
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> CheckIn(int id, CheckInViewModel model)
    {
        var form = await _context.AssignmentForms.Include(f => f.Device).FirstOrDefaultAsync(f => f.Id == id);
        if (form == null)
        {
            return NotFound();
        }
        if (form.ReturnedAt != null)
        {
            return RedirectToDevice(form);
        }

        if (model.Condition is DeviceCondition condition && !Enum.IsDefined(condition))
        {
            ModelState.AddModelError(nameof(model.Condition), "Geçerli bir kontrol sonucu seçin.");
        }
        if (!CheckInStatuses.Contains(model.NewStatus))
        {
            ModelState.AddModelError(nameof(model.NewStatus), "Geçerli bir durum seçin.");
        }
        var signature = SignatureImage.FromDataUrl(model.Signature);
        if (signature == null && ModelState.IsValid)
        {
            ModelState.AddModelError(nameof(model.Signature), "İmza okunamadı, lütfen tekrar imzalayın.");
        }
        if (!ModelState.IsValid)
        {
            model.Form = form;
            return View(model);
        }

        var now = DateTime.UtcNow;
        var receiverId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var receiver = receiverId != null ? await _context.Users.FindAsync(receiverId) : null;

        form.ReturnedAt = now;
        form.ReturnCondition = model.Condition;
        form.ReturnCheckNote = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        form.ReceivedById = receiver?.Id;
        form.ReceivedByName = receiver != null ? $"{receiver.FirstName} {receiver.LastName}" : null;
        form.ReceiverSignature = signature;

        // The device leaves the employee only if it is still theirs; a later reassignment already closed the form
        var device = form.Device;
        if (device != null && device.AssignedUserId == form.UserId)
        {
            var checkNote = form.ReturnCheckNote != null ? $", {form.ReturnCheckNote}" : "";
            AddEvent(device, DeviceEventType.AssignmentChanged,
                $"Zimmet: {form.EmployeeName} → Depo (atanmamış). İade kontrolü: {model.Condition!.Value.GetDisplayName()}{checkNote}",
                receiverId, now);
            if (device.Status != model.NewStatus)
            {
                AddEvent(device, DeviceEventType.StatusChanged,
                    $"Durum: {device.Status.GetDisplayName()} → {model.NewStatus.GetDisplayName()}", receiverId, now);
            }

            device.AssignedUserId = null;
            device.Status = model.NewStatus;
            device.LastUpdated = now;
        }

        if (form.UserId != receiverId)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = form.UserId,
                Message = $"{form.Hostname} cihazınız BT tarafından teslim alındı; zimmetiniz kapandı.",
                Url = "/MyDevices",
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync();

        TempData["FormMessage"] = $"{form.Hostname} teslim alındı, {form.FormNumber} numaralı zimmet kapandı.";
        return RedirectToDevice(form);
    }

    private void AddEvent(Device device, DeviceEventType type, string description, string? performedById, DateTime now)
    {
        _context.DeviceEvents.Add(new DeviceEvent
        {
            Device = device,
            Type = type,
            // DeviceEvent.Description holds at most 500 characters and the check note alone can be that long
            Description = description.Length > 500 ? description[..499] + "…" : description,
            EventDate = now,
            PerformedById = performedById,
            CreatedAt = now
        });
    }

    // Back to the device's zimmet forms, or to the device list when the device was deleted
    private IActionResult RedirectToDevice(AssignmentForm form) => form.DeviceId is int deviceId
        ? Redirect(Url.Action("Details", "Devices", new { id = deviceId }) + "#assignment-forms")
        : RedirectToAction("Index", "Devices");
}
