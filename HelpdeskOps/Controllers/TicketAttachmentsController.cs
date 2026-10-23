using System.Security.Claims;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Upload and download of ticket attachments
[Authorize]
public class TicketAttachmentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AttachmentStorage _storage;

    public TicketAttachmentsController(ApplicationDbContext context, AttachmentStorage storage)
    {
        _context = context;
        _storage = storage;
    }

    // Same rule as the ticket details page: IT staff and managers see every ticket, others only their own
    private bool CanRead(Ticket ticket) =>
        User.IsInRole("ITAdmin") || User.IsInRole("Manager")
        || ticket.RequesterId == User.FindFirstValue(ClaimTypes.NameIdentifier);

    // Same rule as comments: the requester and IT staff take part in the ticket
    private bool CanWrite(Ticket ticket) =>
        User.IsInRole("ITAdmin") || ticket.RequesterId == User.FindFirstValue(ClaimTypes.NameIdentifier);

    // POST: TicketAttachments/Upload/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(AttachmentStorage.MaxFileSize * AttachmentStorage.MaxFilesPerUpload + 1024 * 1024)]
    public async Task<IActionResult> Upload(int id, List<IFormFile> files)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null)
        {
            return NotFound();
        }

        if (!CanWrite(ticket))
        {
            return Forbid();
        }

        var error = files.Count == 0 ? "Lütfen en az bir dosya seçin." : AttachmentStorage.ValidateAll(files);
        if (error != null)
        {
            TempData["AttachmentError"] = error;
        }
        else
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            foreach (var file in files)
            {
                var attachment = await _storage.SaveAsync(file, userId);
                attachment.TicketId = id;
                _context.TicketAttachments.Add(attachment);
            }
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Redirect(Url.Action("Details", "Tickets", new { id }) + "#attachments");
    }

    // GET: TicketAttachments/Download/5
    // Always sent as a download with the type fixed by the extension, so an uploaded file is never rendered as a page
    public async Task<IActionResult> Download(int id)
    {
        var attachment = await _context.TicketAttachments
            .Include(a => a.Ticket)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
        if (attachment?.Ticket == null)
        {
            return NotFound();
        }

        if (!CanRead(attachment.Ticket))
        {
            return Forbid();
        }

        var stream = _storage.OpenRead(attachment);
        if (stream == null)
        {
            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, attachment.ContentType, attachment.FileName);
    }
}
