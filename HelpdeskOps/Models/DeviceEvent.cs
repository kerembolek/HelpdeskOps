using System.ComponentModel.DataAnnotations;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models;

// One entry in a device's lifecycle: maintenance done by IT, or an assignment or status change
public class DeviceEvent
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    [Display(Name = "İşlem Türü")]
    public DeviceEventType Type { get; set; }

    [Required(ErrorMessage = "Açıklama zorunludur.")]
    [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
    [Display(Name = "Açıklama")]
    public required string Description { get; set; }

    // When the work was done (UTC); can be earlier than CreatedAt for work logged afterwards
    [Display(Name = "İşlem Tarihi")]
    public DateTime EventDate { get; set; } = DateTime.UtcNow;

    // Null for entries created by the system (e.g. demo data)
    public string? PerformedById { get; set; }

    [Display(Name = "İşlemi Yapan")]
    public ApplicationUser? PerformedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
