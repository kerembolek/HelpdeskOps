using System.ComponentModel.DataAnnotations;

namespace HelpdeskOps.Models

{
    public class Device
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "Hostname alanı boş bırakılamaz.")]
        [Display(Name = "Cihaz Adı")]
        public string Hostname { get; set; }
        [Display(Name = "IP Adresi (IPv4)")]
        [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$", ErrorMessage = "Lütfen geçerli bir IPv4 adresi giriniz.")]
        public string IpAddress { get; set; }
        [Required(ErrorMessage = "Seri Numarası zorunludur.")]
        [Display(Name = "Seri Numarası")]
        public string SerialNumber { get; set; }
        [Required(ErrorMessage = "Cihaz Türü seçilmelidir.")]
        [Display(Name = "Cihaz Türü")]
        public string DeviceType { get; set; }
        [Display(Name = "Garanti Bitiş Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? WarrantyEndDate { get; set; }
        [Required(ErrorMessage = "Cihaz durumu belirtilmelidir.")]
        [Display(Name = "Cihaz Durumu")]
        public string Status { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
