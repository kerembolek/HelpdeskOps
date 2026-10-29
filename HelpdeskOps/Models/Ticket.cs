using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models;

public class Ticket
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Talep başlığı zorunludur.")]
    [StringLength(200)]
    [Display(Name = "Başlık")]
    public required string Title { get; set; }

    [Required(ErrorMessage = "Lütfen sorunu veya talebi detaylı açıklayın.")]
    [Display(Name = "Açıklama")]
    public required string Description { get; set; }

    [Required(ErrorMessage = "Kategori seçilmelidir.")]
    [Display(Name = "Kategori")]
    public TicketCategory Category { get; set; }

    // Null only on tickets opened before subcategories existed
    [Display(Name = "Alt Kategori")]
    public TicketSubcategory? Subcategory { get; set; }

    // Filled in when the subcategory is "Other"
    [StringLength(100, ErrorMessage = "En fazla 100 karakter girebilirsiniz.")]
    [Display(Name = "Diğer (belirtiniz)")]
    public string? OtherCategoryText { get; set; }

    // "Donanım › Monitör / ekran", or "Ağ › Telefon santrali" when "Other" was described
    [NotMapped]
    public string CategoryPath => Subcategory switch
    {
        null => Category.GetDisplayName(),
        TicketSubcategory.Other when !string.IsNullOrEmpty(OtherCategoryText) => $"{Category.GetDisplayName()} › {OtherCategoryText}",
        _ => $"{Category.GetDisplayName()} › {Subcategory.Value.GetDisplayName()}"
    };

    [Required(ErrorMessage = "Öncelik seçilmelidir.")]
    [Display(Name = "Öncelik")]
    public TicketPriority Priority { get; set; }

    [Display(Name = "Durum")]
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    [Display(Name = "Oluşturulma Tarihi")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Güncellenme Tarihi")]
    public DateTime? UpdatedAt { get; set; }

    [Display(Name = "Çözülme Tarihi")]
    public DateTime? ResolvedAt { get; set; }

    // When the ticket should be resolved by; set on save from the priority and working hours (TicketSla)
    [Display(Name = "SLA Hedefi")]
    public DateTime? SlaDueAt { get; set; }

    [Display(Name = "Cihaz")]
    public int? DeviceId { get; set; }

    [Display(Name = "Cihaz")]
    public Device? Device { get; set; }

    // Ticket'ı kim açtı
    [Required]
    [Display(Name = "Talep Eden")]
    public required string RequesterId { get; set; }

    [Display(Name = "Talep Eden")]
    public ApplicationUser? Requester { get; set; }

     // Ticket'ı kim üstlendi (IT uzmanı)
    [Display(Name = "Atanan Kişi")]
    public string? AssignedToId { get; set; }

    [Display(Name = "Atanan Kişi")]
    public ApplicationUser? AssignedTo { get; set; }

    // Requester's satisfaction with a resolved or closed ticket, given once
    [Range(1, 5)]
    [Display(Name = "Memnuniyet")]
    public int? SatisfactionRating { get; set; }

    [StringLength(500)]
    [Display(Name = "Değerlendirme Yorumu")]
    public string? SatisfactionComment { get; set; }

    public DateTime? RatedAt { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketActivity> Activities { get; set; } = new List<TicketActivity>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
}