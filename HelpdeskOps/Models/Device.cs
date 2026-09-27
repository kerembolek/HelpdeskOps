using System.ComponentModel.DataAnnotations;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models
{
    public class Device
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Hostname alanı boş bırakılamaz.")]
        [Display(Name = "Cihaz Adı")]
        public required string Hostname { get; set; }

        [Required(ErrorMessage = "IP adresi zorunludur.")]
        [Display(Name = "IP Adresi (IPv4)")]
        [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$", ErrorMessage = "Lütfen geçerli bir IPv4 adresi giriniz.")]
        public required string IpAddress { get; set; }

        [Required(ErrorMessage = "Seri Numarası zorunludur.")]
        [Display(Name = "Seri Numarası")]
        public required string SerialNumber { get; set; }

        [Display(Name = "Marka")]
        public string? Brand { get; set; }

        [Display(Name = "Model")]
        public string? Model { get; set; }

        [Required(ErrorMessage = "Cihaz Türü seçilmelidir.")]
        [Display(Name = "Cihaz Türü")]
        public DeviceType DeviceType { get; set; }

        [Display(Name = "Garanti Bitiş Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? WarrantyEndDate { get; set; }

        [Required(ErrorMessage = "Cihaz durumu belirtilmelidir.")]
        [Display(Name = "Cihaz Durumu")]
        public DeviceStatus Status { get; set; }

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        // Zimmet ilişkisi: bu cihaz kime atanmış
        public string? AssignedUserId { get; set; }
        public ApplicationUser? AssignedUser { get; set; }
    }
}