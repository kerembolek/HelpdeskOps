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

    // Second level under TicketCategory; TicketCategories lists which ones belong to each category.
    // Values are stored as ints, so new subcategories go at the end.
    public enum TicketSubcategory
    {
        [Display(Name = "Diğer")] Other,

        [Display(Name = "Bilgisayar açılmıyor")] PcWontStart,
        [Display(Name = "Yavaşlık / donma")] SlowOrFreezing,
        [Display(Name = "Monitör / ekran")] Display,
        [Display(Name = "Klavye / fare")] KeyboardMouse,
        [Display(Name = "Parça arızası")] PartFailure,

        [Display(Name = "Program kurulumu")] Installation,
        [Display(Name = "Program hata veriyor")] AppError,
        [Display(Name = "Lisans / aktivasyon")] License,
        [Display(Name = "Windows güncellemesi")] WindowsUpdate,

        [Display(Name = "İnternet yok")] NoInternet,
        [Display(Name = "Wi-Fi")] WiFi,
        [Display(Name = "VPN")] Vpn,
        [Display(Name = "Paylaşılan klasör erişimi")] SharedFolder,

        [Display(Name = "Outlook açılmıyor")] OutlookWontOpen,
        [Display(Name = "E-posta gönderme / alma")] SendReceive,
        [Display(Name = "Posta kutusu dolu")] MailboxFull,
        [Display(Name = "Yeni hesap / posta kutusu")] NewMailbox,

        [Display(Name = "Yazdırmıyor")] NotPrinting,
        [Display(Name = "Kağıt sıkışması")] PaperJam,
        [Display(Name = "Toner / kartuş")] Toner,
        [Display(Name = "Tarama")] Scanning
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

    // AssignmentChanged, StatusChanged, and PortChanged are recorded automatically; the rest are entered by IT staff.
    // Values are stored as ints, so new types go at the end.
    public enum DeviceEventType
    {
        [Display(Name = "Format / Yeniden Kurulum")] Format,
        [Display(Name = "Donanım Yükseltme")] HardwareUpgrade,
        [Display(Name = "Parça Değişimi")] PartReplacement,
        [Display(Name = "Onarım / Bakım")] Repair,
        [Display(Name = "Zimmet Değişikliği")] AssignmentChanged,
        [Display(Name = "Durum Değişikliği")] StatusChanged,
        [Display(Name = "Diğer")] Other,
        [Display(Name = "Port Değişikliği")] PortChanged
    }

    // Why an employee hands a device back
    public enum ReturnReason
    {
        [Display(Name = "İşten ayrılış")] Leaving,
        [Display(Name = "Cihaz değişimi")] Replacement,
        [Display(Name = "Arıza")] Faulty,
        [Display(Name = "Görev değişikliği")] RoleChange,
        [Display(Name = "Diğer")] Other
    }

    // What IT finds when it checks a returned device
    public enum DeviceCondition
    {
        [Display(Name = "Sağlam")] Intact,
        [Display(Name = "Hasarlı")] Damaged,
        [Display(Name = "Eksik parça / aksesuar")] MissingParts
    }

    public enum TicketActivityType
    {
        [Display(Name = "Talep oluşturuldu")] Created,
        [Display(Name = "Durum değişti")] StatusChanged,
        [Display(Name = "Öncelik değişti")] PriorityChanged,
        [Display(Name = "Atanan kişi değişti")] AssigneeChanged,
        [Display(Name = "Talep değerlendirildi")] Rated
    }
}