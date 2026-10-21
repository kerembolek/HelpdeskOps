using System.ComponentModel.DataAnnotations;

namespace HelpdeskOps.Models.Enums
{
    public enum DeviceType
    {
        [Display(Name = "Dizüstü Bilgisayar")] Laptop,
        [Display(Name = "Masaüstü Bilgisayar")] Desktop,
        [Display(Name = "Yazıcı")] Printer,
        [Display(Name = "Switch")] Switch,
        [Display(Name = "Router")] Router,
        [Display(Name = "Monitör")] Monitor,
        [Display(Name = "Diğer")] Other
    }

    public enum DeviceStatus
    {
        [Display(Name = "Aktif")] Active,
        [Display(Name = "Depoda")] InStorage,
        [Display(Name = "Arızalı")] Faulty,
        [Display(Name = "Hizmet Dışı")] Retired
    }

    // Derived from WarrantyEndDate, not stored
    public enum WarrantyState
    {
        [Display(Name = "Bilinmiyor")] Unknown,
        [Display(Name = "Garantide")] Active,
        [Display(Name = "Yakında Bitiyor")] ExpiringSoon,
        [Display(Name = "Garanti Bitti")] Expired
    }

    public enum TicketCategory
    {
        [Display(Name = "Donanım")] Hardware,
        [Display(Name = "Yazılım")] Software,
        [Display(Name = "Ağ")] Network,
        [Display(Name = "E-posta / Outlook")] EmailOutlook,
        [Display(Name = "Yazıcı")] Printer,
        [Display(Name = "Diğer")] Other
    }

    public enum TicketPriority
    {
        [Display(Name = "Düşük")] Low,
        [Display(Name = "Orta")] Medium,
        [Display(Name = "Yüksek")] High,
        [Display(Name = "Kritik")] Critical
    }

    public enum TicketStatus
    {
        [Display(Name = "Açık")] Open,
        [Display(Name = "İşlemde")] InProgress,
        [Display(Name = "Çözüldü")] Resolved,
        [Display(Name = "Kapatıldı")] Closed
    }

    // AssignmentChanged and StatusChanged are recorded automatically; the rest are entered by IT staff
    public enum DeviceEventType
    {
        [Display(Name = "Format / Yeniden Kurulum")] Format,
        [Display(Name = "Donanım Yükseltme")] HardwareUpgrade,
        [Display(Name = "Parça Değişimi")] PartReplacement,
        [Display(Name = "Onarım / Bakım")] Repair,
        [Display(Name = "Zimmet Değişikliği")] AssignmentChanged,
        [Display(Name = "Durum Değişikliği")] StatusChanged,
        [Display(Name = "Diğer")] Other
    }

    public enum TicketActivityType
    {
        [Display(Name = "Talep oluşturuldu")] Created,
        [Display(Name = "Durum değişti")] StatusChanged,
        [Display(Name = "Öncelik değişti")] PriorityChanged,
        [Display(Name = "Atanan kişi değişti")] AssigneeChanged
    }
}