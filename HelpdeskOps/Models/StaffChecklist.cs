using System.ComponentModel.DataAnnotations;

namespace HelpdeskOps.Models;

public enum ChecklistType
{
    [Display(Name = "İşe Giriş")] Onboarding,
    [Display(Name = "İşten Çıkış")] Offboarding
}

// An onboarding or offboarding run for one person. The manual steps are stored as items; the steps HelpdeskOps can
// see for itself (role, privacy notice, devices, zimmet forms, tickets) are checked live by StaffChecklistRules.
public class StaffChecklist
{
    public int Id { get; set; }

    [Display(Name = "Süreç")]
    public ChecklistType Type { get; set; }

    // The person joining or leaving
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // First working day for onboarding, last working day for offboarding; a calendar date
    [Display(Name = "Hedef Tarih")]
    [DataType(DataType.Date)]
    public DateTime TargetDate { get; set; }

    [StringLength(500)]
    [Display(Name = "Not")]
    public string? Note { get; set; }

    [Display(Name = "Açılış Tarihi")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    [Display(Name = "Tamamlanma Tarihi")]
    public DateTime? CompletedAt { get; set; }

    public string? CompletedById { get; set; }
    public ApplicationUser? CompletedBy { get; set; }

    public ICollection<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();

    public bool IsOverdue(DateTime today) => CompletedAt == null && TargetDate.Date < today;
}

// A step IT ticks off by hand, such as opening the e-mail account or collecting the access card
public class ChecklistItem
{
    public int Id { get; set; }

    public int StaffChecklistId { get; set; }
    public StaffChecklist? Checklist { get; set; }

    [Required(ErrorMessage = "Adım metni zorunludur.")]
    [StringLength(200, ErrorMessage = "Adım en fazla 200 karakter olabilir.")]
    [Display(Name = "Adım")]
    public required string Title { get; set; }

    public int SortOrder { get; set; }

    [Display(Name = "Yapılma Tarihi")]
    public DateTime? DoneAt { get; set; }

    public string? DoneById { get; set; }
    public ApplicationUser? DoneBy { get; set; }
}
