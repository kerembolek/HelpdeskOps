using System.ComponentModel.DataAnnotations;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models;

// One entry in a ticket's history: creation, or a change of status, priority, or assignee.
// Old and new values are stored as display text so the history stays readable
// even if the related user is renamed or removed later.
public class TicketActivity
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    // Null for changes made by the system (e.g. demo data)
    public string? ActorId { get; set; }

    [Display(Name = "İşlemi Yapan")]
    public ApplicationUser? Actor { get; set; }

    [Display(Name = "İşlem")]
    public TicketActivityType Type { get; set; }

    [StringLength(200)]
    public string? OldValue { get; set; }

    [StringLength(200)]
    public string? NewValue { get; set; }

    [Display(Name = "Tarih")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
