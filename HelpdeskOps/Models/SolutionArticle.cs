using System.ComponentModel.DataAnnotations;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models;

// Tier 0 self-service: troubleshooting steps users try before opening a ticket
public class SolutionArticle
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur.")]
    [StringLength(150, ErrorMessage = "Başlık en fazla 150 karakter olabilir.")]
    [Display(Name = "Başlık")]
    public required string Title { get; set; }

    [Required(ErrorMessage = "Kategori seçilmelidir.")]
    [Display(Name = "Kategori")]
    public TicketCategory Category { get; set; }

    [StringLength(300, ErrorMessage = "Belirtiler en fazla 300 karakter olabilir.")]
    [Display(Name = "Belirtiler")]
    public string? Symptoms { get; set; }

    // One step per line; shown as a numbered list
    [Required(ErrorMessage = "En az bir adım yazılmalıdır.")]
    [StringLength(4000, ErrorMessage = "Adımlar en fazla 4000 karakter olabilir.")]
    [Display(Name = "Çözüm Adımları")]
    public required string Steps { get; set; }

    [Display(Name = "Yayında")]
    public bool IsPublished { get; set; } = true;

    // Users who said the steps fixed their problem vs. went on to open a ticket
    [Display(Name = "Çözüldü")]
    public int SolvedCount { get; set; }

    [Display(Name = "Talebe Dönüştü")]
    public int EscalatedCount { get; set; }

    [Display(Name = "Oluşturulma Tarihi")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Son Güncelleme")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public IEnumerable<string> GetStepList() =>
        Steps.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Null until someone has used the article
    public double? GetSelfServiceRate()
    {
        var total = SolvedCount + EscalatedCount;
        return total == 0 ? null : (double)SolvedCount / total;
    }
}
