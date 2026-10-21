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

        [Display(Name = "Satın Alma Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? PurchaseDate { get; set; }

        [Display(Name = "Garanti Bitiş Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? WarrantyEndDate { get; set; }

        // Service / vendor contact used for warranty claims and repairs
        [Display(Name = "Servis / Tedarikçi Firma")]
        [StringLength(100, ErrorMessage = "Firma adı en fazla 100 karakter olabilir.")]
        public string? VendorName { get; set; }

        [Display(Name = "Servis Telefonu")]
        [Phone(ErrorMessage = "Lütfen geçerli bir telefon numarası giriniz.")]
        [StringLength(30, ErrorMessage = "Telefon numarası en fazla 30 karakter olabilir.")]
        public string? VendorPhone { get; set; }

        [Display(Name = "Servis E-postası")]
        [EmailAddress(ErrorMessage = "Lütfen geçerli bir e-posta adresi giriniz.")]
        [StringLength(100, ErrorMessage = "E-posta en fazla 100 karakter olabilir.")]
        public string? VendorEmail { get; set; }

        [Required(ErrorMessage = "Cihaz durumu belirtilmelidir.")]
        [Display(Name = "Cihaz Durumu")]
        public DeviceStatus Status { get; set; }

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Zimmet ilişkisi: bu cihaz kime atanmış
        public string? AssignedUserId { get; set; }
        public ApplicationUser? AssignedUser { get; set; }

        public ICollection<DeviceEvent> Events { get; set; } = new List<DeviceEvent>();

        // Switch port mapping: which switch port this device is patched into
        [Display(Name = "Bağlı Olduğu Switch")]
        public int? SwitchId { get; set; }
        public Device? Switch { get; set; }

        // Cisco interface name, e.g. Gi1/0/12 or Fa0/5
        [Display(Name = "Switch Portu")]
        [StringLength(20)]
        [RegularExpression(@"^(Fa|Gi|Te|Tw|Fo|Hu|Eth)\d+(/\d+){1,2}$", ErrorMessage = "Port Cisco biçiminde olmalıdır, örn. Gi1/0/12 veya Fa0/5.")]
        public string? SwitchPort { get; set; }

        [Display(Name = "VLAN")]
        [Range(1, 4094, ErrorMessage = "VLAN 1 ile 4094 arasında olmalıdır.")]
        public int? VlanId { get; set; }

        // Devices patched into this switch (only used when DeviceType is Switch)
        public ICollection<Device> ConnectedDevices { get; set; } = new List<Device>();

        // Remote Desktop only makes sense for computers
        public bool SupportsRemoteDesktop() => DeviceType is DeviceType.Laptop or DeviceType.Desktop;

        // Warranty dates are calendar dates, so they are compared with the local date
        public const int WarrantyExpiringSoonDays = 30;

        public WarrantyState GetWarrantyState()
        {
            if (WarrantyEndDate is not DateTime end)
                return WarrantyState.Unknown;
            if (end.Date < DateTime.Today)
                return WarrantyState.Expired;
            return end.Date <= DateTime.Today.AddDays(WarrantyExpiringSoonDays)
                ? WarrantyState.ExpiringSoon
                : WarrantyState.Active;
        }
    }
}