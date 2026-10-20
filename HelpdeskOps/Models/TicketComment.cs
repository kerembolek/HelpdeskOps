using System.ComponentModel.DataAnnotations;

namespace HelpdeskOps.Models;

// A message written on a ticket by the requester or by IT staff
public class TicketComment
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    [Required]
    public required string AuthorId { get; set; }

    [Display(Name = "Yazan")]
    public ApplicationUser? Author { get; set; }

    [Required(ErrorMessage = "Yorum boş olamaz.")]
    [StringLength(2000, ErrorMessage = "Yorum en fazla 2000 karakter olabilir.")]
    [Display(Name = "Yorum")]
    public required string Body { get; set; }

    [Display(Name = "Tarih")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
