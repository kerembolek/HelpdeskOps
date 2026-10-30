using System.ComponentModel.DataAnnotations;

namespace HelpdeskOps.Models;

public enum InboundEmailResult
{
    TicketCreated,
    CommentAdded,
    Skipped
}

// One message read from the helpdesk mailbox. It keeps a mail from being processed twice if the mailbox could not be
// updated, and it limits how much one sender can open. The content itself lives on the ticket, not here.
public class InboundEmail
{
    public int Id { get; set; }

    // SHA-256 of the Message-ID header
    [Required]
    [StringLength(64)]
    public required string MessageHash { get; set; }

    public string? UserId { get; set; }

    public int? TicketId { get; set; }

    public InboundEmailResult Result { get; set; }

    [StringLength(100)]
    public string? SkipReason { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
