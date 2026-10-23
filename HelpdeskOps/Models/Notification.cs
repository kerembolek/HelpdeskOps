using System.ComponentModel.DataAnnotations;

namespace HelpdeskOps.Models;

// An in-app message for one user, e.g. "Talebiniz çözüldü", linking to the page it is about
public class Notification
{
    public int Id { get; set; }

    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [StringLength(300)]
    public required string Message { get; set; }

    // Local path such as /Tickets/Details/5; only local paths are ever followed
    [StringLength(200)]
    public required string Url { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }
}
