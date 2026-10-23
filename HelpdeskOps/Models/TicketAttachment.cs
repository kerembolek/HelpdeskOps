using System.ComponentModel.DataAnnotations;

namespace HelpdeskOps.Models;

// A file (screenshot, log, document) added to a ticket. The file itself lives on disk under StoredName.
public class TicketAttachment
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    // Original file name as uploaded, cleaned of path parts; only used for display and download
    [StringLength(200)]
    public required string FileName { get; set; }

    // Random name on disk, so user input never becomes part of a file path
    [StringLength(64)]
    public required string StoredName { get; set; }

    [StringLength(100)]
    public required string ContentType { get; set; }

    public long Size { get; set; }

    public string? UploadedById { get; set; }
    public ApplicationUser? UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
