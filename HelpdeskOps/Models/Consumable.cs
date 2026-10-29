using System.ComponentModel.DataAnnotations;

namespace HelpdeskOps.Models;

public enum ConsumableKind
{
    [Display(Name = "Toner")] Toner,
    [Display(Name = "Kartuş")] Cartridge,
    [Display(Name = "Drum / Tambur")] Drum,
    [Display(Name = "Kağıt")] Paper,
    [Display(Name = "Diğer")] Other
}

// A consumable kept in stock, such as a printer's toner. The stock changes only through movements,
// so every box that came in or went out has a date, a person, and (for usage) the printer it went to.
public class Consumable
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad zorunludur.")]
    [StringLength(100, ErrorMessage = "Ad en fazla 100 karakter olabilir.")]
    [Display(Name = "Ad")]
    public required string Name { get; set; }

    [Display(Name = "Tür")]
    public ConsumableKind Kind { get; set; }

    [StringLength(200, ErrorMessage = "Uyumlu modeller en fazla 200 karakter olabilir.")]
    [Display(Name = "Uyumlu Modeller")]
    public string? CompatibleModels { get; set; }

    // Server-owned: changed only by recording a movement
    [Display(Name = "Stok")]
    public int Stock { get; set; }

    [Range(0, 10000, ErrorMessage = "En az stok 0 ile 10000 arasında olmalıdır.")]
    [Display(Name = "En Az Stok")]
    public int MinimumStock { get; set; } = 1;

    [StringLength(100, ErrorMessage = "Yer en fazla 100 karakter olabilir.")]
    [Display(Name = "Bulunduğu Yer")]
    public string? Location { get; set; }

    [Display(Name = "Oluşturulma Tarihi")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConsumableMovement> Movements { get; set; } = new List<ConsumableMovement>();

    // At or below the minimum it is time to order
    public bool IsLow => Stock <= MinimumStock;
}

// Stock coming in (positive) or going out to a printer or person (negative)
public class ConsumableMovement
{
    public int Id { get; set; }

    [Display(Name = "Sarf Malzemesi")]
    public int ConsumableId { get; set; }
    public Consumable? Consumable { get; set; }

    [Display(Name = "Miktar")]
    public int Quantity { get; set; }

    // The printer a toner went into; null for stock coming in
    [Display(Name = "Cihaz")]
    public int? DeviceId { get; set; }
    public Device? Device { get; set; }

    [StringLength(200)]
    [Display(Name = "Not")]
    public string? Note { get; set; }

    [Display(Name = "Tarih")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
}

public static class ConsumableStock
{
    public const int MaxQuantity = 1000;

    // Why the movement cannot be recorded, or null when it can. Stock never goes below zero.
    public static string? Check(Consumable consumable, int quantity)
    {
        if (quantity == 0 || Math.Abs(quantity) > MaxQuantity)
            return $"Miktar 1 ile {MaxQuantity} arasında olmalıdır.";
        if (consumable.Stock + quantity < 0)
            return $"Stokta yalnızca {consumable.Stock} adet var.";
        return null;
    }
}
