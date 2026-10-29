using System.ComponentModel.DataAnnotations;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models;

// A software license the company pays for (Microsoft 365, an accounting program, antivirus) and how many people
// may use it. License keys are deliberately not stored here: they belong in a password vault, not a helpdesk.
public class SoftwareLicense
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Lisans adı zorunludur.")]
    [StringLength(100, ErrorMessage = "Lisans adı en fazla 100 karakter olabilir.")]
    [Display(Name = "Lisans Adı")]
    public required string Name { get; set; }

    [StringLength(100, ErrorMessage = "Üretici en fazla 100 karakter olabilir.")]
    [Display(Name = "Üretici / Tedarikçi")]
    public string? Vendor { get; set; }

    [Range(1, 100000, ErrorMessage = "Kullanıcı sayısı 1 ile 100000 arasında olmalıdır.")]
    [Display(Name = "Kullanıcı Sayısı")]
    public int Seats { get; set; } = 1;

    // Subscription renewal or support end; empty for a perpetual license
    [DataType(DataType.Date)]
    [Display(Name = "Yenileme / Bitiş Tarihi")]
    public DateTime? RenewalDate { get; set; }

    [StringLength(500, ErrorMessage = "Not en fazla 500 karakter olabilir.")]
    [Display(Name = "Not")]
    public string? Notes { get; set; }

    [Display(Name = "Oluşturulma Tarihi")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LicenseAssignment> Assignments { get; set; } = new List<LicenseAssignment>();

    // Same window as device warranties, so both kinds of renewal show up together
    public WarrantyState GetRenewalState(DateTime today)
    {
        if (RenewalDate is not DateTime end)
            return WarrantyState.Unknown;
        if (end.Date < today)
            return WarrantyState.Expired;
        return end.Date <= today.AddDays(Device.WarrantyExpiringSoonDays) ? WarrantyState.ExpiringSoon : WarrantyState.Active;
    }
}

// One seat of a license given to one person
public class LicenseAssignment
{
    public int Id { get; set; }

    [Display(Name = "Lisans")]
    public int SoftwareLicenseId { get; set; }
    public SoftwareLicense? License { get; set; }

    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Display(Name = "Atanma Tarihi")]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public string? AssignedById { get; set; }
    public ApplicationUser? AssignedBy { get; set; }
}

public record LicenseListRow(int Id, string Name, string? Vendor, int Seats, int Used, DateTime? RenewalDate);
