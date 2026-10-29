namespace HelpdeskOps.Models;

// What HelpdeskOps already knows about the person, read from accounts, devices, zimmet forms, and tickets
public record ChecklistFacts(
    bool HasRole,
    bool ReadPrivacyNotice,
    int AssignedDevices,
    int UnsignedForms,
    int OpenForms,
    int ActiveAssignedTickets,
    int ReturnsWithoutSignature);

// One automatic check. A required check has to pass before the checklist can be completed; the others only warn.
public record ChecklistCheck(string Title, bool Passed, string Detail, bool Required = true);

// The default manual steps and the automatic checks for each kind of checklist
public static class StaffChecklistRules
{
    public static IReadOnlyList<string> DefaultItems(ChecklistType type) => type switch
    {
        ChecklistType.Onboarding => new[]
        {
            "Windows / Active Directory hesabı açıldı",
            "Kurumsal e-posta hesabı açıldı",
            "Gerekli programlar ve lisanslar kuruldu",
            "Paylaşılan klasör ve yazıcı erişimi verildi",
            "Kartlı geçiş kartı hazırlandı",
            "İlk gün BT tanıtımı yapıldı (şifre, iki adımlı doğrulama, talep açma)"
        },
        _ => new[]
        {
            "Windows / Active Directory hesabı kapatıldı",
            "E-posta yönlendirmesi veya otomatik yanıt ayarlandı",
            "Lisanslar geri alındı",
            "VPN, uzak erişim ve paylaşılan klasör yetkileri kaldırıldı",
            "Kartlı geçiş kartı teslim alındı"
        }
    };

    public static IReadOnlyList<ChecklistCheck> Checks(ChecklistType type, ChecklistFacts facts) => type switch
    {
        ChecklistType.Onboarding => new[]
        {
            new ChecklistCheck("Rol verildi", facts.HasRole,
                facts.HasRole ? "Hesap açık ve bir rolü var." : "Kullanıcılar sayfasından bir rol verin."),
            new ChecklistCheck("Aydınlatma metni okundu", facts.ReadPrivacyNotice,
                facts.ReadPrivacyNotice ? "Güncel KVKK aydınlatma metnini onayladı." : "Çalışan ilk girişinde ana sayfadan onaylar."),
            new ChecklistCheck("Cihaz zimmetlendi", facts.AssignedDevices > 0,
                facts.AssignedDevices > 0 ? $"{facts.AssignedDevices} cihaz zimmetli." : "Cihazlar sayfasından en az bir cihaz atayın."),
            new ChecklistCheck("Zimmet formları imzalandı", facts.AssignedDevices > 0 && facts.UnsignedForms == 0,
                facts.UnsignedForms > 0 ? $"{facts.UnsignedForms} form çalışanın onayını bekliyor (Cihazlarım)."
                    : facts.AssignedDevices > 0 ? "Tüm cihazlar imzayla teslim alındı." : "Önce cihaz zimmetlenmeli.")
        },
        _ => new[]
        {
            new ChecklistCheck("Zimmetli cihaz kalmadı", facts.AssignedDevices == 0,
                facts.AssignedDevices == 0 ? "Tüm cihazlar geri alındı." : $"{facts.AssignedDevices} cihaz hâlâ zimmetli."),
            new ChecklistCheck("Zimmet formları kapandı", facts.OpenForms == 0,
                facts.OpenForms == 0 ? "Açık zimmet formu yok." : $"{facts.OpenForms} form iade edilmedi."),
            new ChecklistCheck("Üzerinde aktif talep yok", facts.ActiveAssignedTickets == 0,
                facts.ActiveAssignedTickets == 0 ? "Atanmış açık talebi yok." : $"{facts.ActiveAssignedTickets} aktif talebi başka birine atayın."),
            // A device taken back without a signed check-in still counts as returned, so this only warns
            new ChecklistCheck("İadeler imzayla teslim alındı", facts.ReturnsWithoutSignature == 0,
                facts.ReturnsWithoutSignature == 0 ? "Bu süreçteki iadelerin hepsi BT imzalı."
                    : $"{facts.ReturnsWithoutSignature} cihaz imzalı teslim alma olmadan geri alındı.", Required: false)
        }
    };

    // Why the checklist cannot be completed yet, or null when it can
    public static string? CompletionBlocker(StaffChecklist checklist, IReadOnlyList<ChecklistCheck> checks)
    {
        if (checklist.CompletedAt != null)
            return "Bu liste zaten tamamlandı.";

        var failed = checks.Count(c => c.Required && !c.Passed);
        if (failed > 0)
            return $"{failed} otomatik kontrol henüz geçmedi.";

        var open = checklist.Items.Count(i => i.DoneAt == null);
        if (open > 0)
            return $"{open} adım henüz işaretlenmedi.";

        return null;
    }
}
