using System.ComponentModel.DataAnnotations;

namespace HelpdeskOps.Models;

public enum AuditAction
{
    [Display(Name = "Oluşturuldu")] Created,
    [Display(Name = "Değiştirildi")] Updated,
    [Display(Name = "Silindi")] Deleted
}

// One saved change to a device, ticket, zimmet form, article, attachment, user, or role, written automatically by
// AuditInterceptor. Who did it and every value are copied as text, so a record stays readable after the user or the
// record itself is deleted.
public class AuditLog
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Null for changes made by the system, such as the demo data seeder at startup
    [StringLength(450)]
    public string? UserId { get; set; }

    [StringLength(256)]
    public string? UserName { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    public AuditAction Action { get; set; }

    // The CLR type name, e.g. "Device"
    [StringLength(100)]
    public required string EntityType { get; set; }

    [StringLength(450)]
    public string? EntityId { get; set; }

    // A short label for the record, e.g. a device's hostname or a ticket's title
    [StringLength(300)]
    public string? EntityLabel { get; set; }

    // JSON array of { "Field", "Old", "New" } with display names and display values
    public string? Changes { get; set; }
}

public record AuditChange(string Field, string? Old, string? New);
