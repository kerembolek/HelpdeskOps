using System.ComponentModel.DataAnnotations;
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

    [Required(ErrorMessage = "Öncelik seçilmelidir.")]
    [Display(Name = "Öncelik")]
    public TicketPriority Priority { get; set; }

    [Display(Name = "Durum")]
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    [Display(Name = "Oluşturulma Tarihi")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "Güncellenme Tarihi")]
    public DateTime? UpdatedAt { get; set; }

    [Display(Name = "Çözülme Tarihi")]
    public DateTime? ResolvedAt { get; set; }

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
}