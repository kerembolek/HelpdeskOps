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
}