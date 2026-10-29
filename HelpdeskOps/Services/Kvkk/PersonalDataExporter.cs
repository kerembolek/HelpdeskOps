using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Services.Kvkk;

// Collects everything HelpdeskOps keeps about one person into a readable JSON file, for a KVKK article 11 request.
// The person downloads it from "Hesabım", or IT downloads it for them from the user list; either way the audit log
// records who exported whose data.
public class PersonalDataExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Keeps Turkish letters readable in the file; it is downloaded, never put into a page
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ApplicationDbContext _context;

    public PersonalDataExporter(ApplicationDbContext context)
    {
        _context = context;
    }

    // Returns null when there is no such user
    public async Task<byte[]?> ExportAsync(string userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return null;
        }

        var roles = await (
                from userRole in _context.UserRoles
                join role in _context.Roles on userRole.RoleId equals role.Id
                where userRole.UserId == userId
                select role.Name)
            .ToListAsync();

        var requested = await _context.Tickets.AsNoTracking()
            .Where(t => t.RequesterId == userId)
            .OrderBy(t => t.Id)
            .ToListAsync();

        var assigned = await _context.Tickets.AsNoTracking()
            .Where(t => t.AssignedToId == userId)
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Title, t.Status, t.CreatedAt })
            .ToListAsync();

        var comments = await _context.TicketComments.AsNoTracking()
            .Where(c => c.AuthorId == userId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var attachments = await _context.TicketAttachments.AsNoTracking()
            .Where(a => a.UploadedById == userId)
            .OrderBy(a => a.UploadedAt)
            .ToListAsync();

        var devices = await _context.Devices.AsNoTracking()
            .Where(d => d.AssignedUserId == userId)
            .OrderBy(d => d.Hostname)
            .ToListAsync();

        var forms = await _context.AssignmentForms.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.IssuedAt)
            .ToListAsync();

        var licenses = await _context.LicenseAssignments.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.AssignedAt)
            .Select(a => new { a.License!.Name, a.AssignedAt })
            .ToListAsync();

        var checklists = await _context.StaffChecklists.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var notifications = await _context.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();

        var auditEntries = await _context.AuditLogs.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        var data = new Dictionary<string, object?>
        {
            ["Açıklama"] = "HelpdeskOps'ta sizinle ilgili tutulan kişisel veriler (KVKK md. 11). Saatler Türkiye saatidir.",
            ["Oluşturulma"] = Time(DateTime.UtcNow),
            ["Hesap"] = new Dictionary<string, object?>
            {
                ["Ad"] = user.FirstName,
                ["Soyad"] = user.LastName,
                ["E-posta"] = user.Email,
                ["E-posta Onaylı"] = user.EmailConfirmed,
                ["Telefon"] = user.PhoneNumber,
                ["Departman"] = user.Department,
                ["Roller"] = roles.Select(AppRoles.DisplayName).ToList(),
                ["İki Adımlı Doğrulama"] = user.TwoFactorEnabled,
                ["Aydınlatma Metni Sürümü"] = user.PrivacyNoticeVersion,
                ["Aydınlatma Metni Okunma"] = Time(user.PrivacyNoticeReadAt)
            },
            ["Açtığınız Talepler"] = requested.Select(t => new Dictionary<string, object?>
            {
                ["No"] = t.Id,
                ["Başlık"] = t.Title,
                ["Açıklama"] = t.Description,
                ["Kategori"] = t.CategoryPath,
                ["Öncelik"] = t.Priority.GetDisplayName(),
                ["Durum"] = t.Status.GetDisplayName(),
                ["Oluşturulma"] = Time(t.CreatedAt),
                ["Çözülme"] = Time(t.ResolvedAt),
                ["Memnuniyet"] = t.SatisfactionRating,
                ["Değerlendirme Yorumu"] = t.SatisfactionComment
            }).ToList(),
            ["Size Atanan Talepler"] = assigned.Select(t => new Dictionary<string, object?>
            {
                ["No"] = t.Id,
                ["Başlık"] = t.Title,
                ["Durum"] = t.Status.GetDisplayName(),
                ["Oluşturulma"] = Time(t.CreatedAt)
            }).ToList(),
            ["Yorumlarınız"] = comments.Select(c => new Dictionary<string, object?>
            {
                ["Talep No"] = c.TicketId,
                ["Tarih"] = Time(c.CreatedAt),
                ["Yorum"] = c.Body
            }).ToList(),
            ["Yüklediğiniz Dosyalar"] = attachments.Select(a => new Dictionary<string, object?>
            {
                ["Talep No"] = a.TicketId,
                ["Dosya Adı"] = a.FileName,
                ["Boyut (bayt)"] = a.Size,
                ["Tarih"] = Time(a.UploadedAt)
            }).ToList(),
            ["Zimmetli Cihazlar"] = devices.Select(d => new Dictionary<string, object?>
            {
                ["Cihaz Adı"] = d.Hostname,
                ["Tür"] = d.DeviceType.GetDisplayName(),
                ["Marka / Model"] = $"{d.Brand} {d.Model}".Trim(),
                ["Seri No"] = d.SerialNumber
            }).ToList(),
            ["Zimmet Formları"] = forms.Select(f => new Dictionary<string, object?>
            {
                ["Form No"] = f.FormNumber,
                ["Cihaz"] = f.Hostname,
                ["Seri No"] = f.SerialNumber,
                ["Ad Soyad"] = f.EmployeeName,
                ["Departman"] = f.Department,
                ["Teslim"] = Time(f.IssuedAt),
                ["Teslim Eden"] = f.IssuedByName,
                ["Onay"] = Time(f.AcceptedAt),
                ["Teslim İmzası"] = f.AcceptanceSignature != null ? "Var (PDF'te görünür)" : null,
                ["İade Talebi"] = Time(f.ReturnRequestedAt),
                ["İade Sebebi"] = f.ReturnReason?.GetDisplayName(),
                ["İade Açıklaması"] = f.ReturnNote,
                ["İade İmzası"] = f.ReturnSignature != null ? "Var (PDF'te görünür)" : null,
                ["İade"] = Time(f.ReturnedAt)
            }).ToList(),
            ["Yazılım Lisanslarınız"] = licenses.Select(l => new Dictionary<string, object?>
            {
                ["Lisans"] = l.Name,
                ["Atanma"] = Time(l.AssignedAt)
            }).ToList(),
            ["İşe Giriş / İşten Çıkış"] = checklists.Select(c => new Dictionary<string, object?>
            {
                ["Süreç"] = c.Type.GetDisplayName(),
                ["Hedef Tarih"] = c.TargetDate.ToString("dd.MM.yyyy"),
                ["Not"] = c.Note,
                ["Açılış"] = Time(c.CreatedAt),
                ["Tamamlanma"] = Time(c.CompletedAt)
            }).ToList(),
            ["Bildirimler"] = notifications.Select(n => new Dictionary<string, object?>
            {
                ["Tarih"] = Time(n.CreatedAt),
                ["Mesaj"] = n.Message,
                ["Okunma"] = Time(n.ReadAt)
            }).ToList(),
            ["Yaptığınız İşlemler (Denetim Kaydı)"] = auditEntries.Select(a => new Dictionary<string, object?>
            {
                ["Tarih"] = Time(a.CreatedAt),
                ["IP Adresi"] = a.IpAddress,
                ["İşlem"] = a.Action.GetDisplayName(),
                ["Kayıt Türü"] = AuditInterceptor.EntityNames.GetValueOrDefault(a.EntityType, a.EntityType),
                ["Kayıt"] = a.EntityLabel
            }).ToList()
        };

        return JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
    }

    // Records the export in the audit log: who downloaded whose data, and from where
    public async Task RecordExportAsync(string userId, HttpContext http)
    {
        var email = await _context.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync();
        _context.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            UserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = http.User.Identity?.Name,
            IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            Action = AuditAction.Exported,
            EntityType = "ApplicationUser",
            EntityId = userId,
            EntityLabel = email,
            Changes = JsonSerializer.Serialize(new[] { new AuditChange("Kişisel Veriler", null, "JSON dosyası olarak indirildi") })
        });
        await _context.SaveChangesAsync();
    }

    // "kisisel-veriler-onur-guclu-2026-10-26.json"
    public static string FileName(ApplicationUser user) =>
        $"kisisel-veriler-{Slug(user.FirstName + " " + user.LastName)}-{TurkeyTime.Today:yyyy-MM-dd}.json";

    private static string Slug(string text)
    {
        var map = new Dictionary<char, char> { ['ç'] = 'c', ['ğ'] = 'g', ['ı'] = 'i', ['ö'] = 'o', ['ş'] = 's', ['ü'] = 'u' };
        var chars = text.Replace('İ', 'i').ToLowerInvariant().Select(c => map.GetValueOrDefault(c, c))
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-');
        var slug = string.Join("-", new string(chars.ToArray()).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length == 0 ? "kullanici" : slug;
    }

    private static string? Time(DateTime? utc) => utc?.ToTurkeyTime().ToString("dd.MM.yyyy HH:mm");
}
