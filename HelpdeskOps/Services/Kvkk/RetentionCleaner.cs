using System.Security.Claims;
using System.Text.Json;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.Kvkk;

// One retention rule as people read it on the privacy notice and the IT page
public record RetentionRule(string Name, int Days, string WhatHappens);

// How many records a run changed, or would change now, per rule
public record RetentionCounts(int Notifications, int Attachments, int AuditIpAddresses, int AuditLogs, int AssignmentForms)
{
    public int Total => Notifications + Attachments + AuditIpAddresses + AuditLogs + AssignmentForms;

    // In the same order as RetentionCleaner.Rules
    public IReadOnlyList<int> InRuleOrder() => new[] { Notifications, Attachments, AuditIpAddresses, AuditLogs, AssignmentForms };
}

// Applies the KVKK retention periods from the "Kvkk:Retention" settings: deletes or erases personal data that has
// been kept as long as its purpose needs. RetentionService runs it once a day; IT can also run it from the
// "Saklama Süreleri" page. Each run that changes anything leaves one summary entry in the audit log.
public class RetentionCleaner
{
    public const string AuditEntityType = "Retention";

    private readonly ApplicationDbContext _context;
    private readonly AttachmentStorage _storage;
    private readonly RetentionOptions _options;

    public RetentionCleaner(ApplicationDbContext context, AttachmentStorage storage, IOptions<KvkkOptions> kvkk)
    {
        _context = context;
        _storage = storage;
        _options = kvkk.Value.Retention;
    }

    public static IReadOnlyList<RetentionRule> Rules(RetentionOptions options) => new[]
    {
        new RetentionRule("Bildirimler", options.NotificationDays, "Okunmuş ya da okunmamış tüm bildirimler silinir."),
        new RetentionRule("Talep ekleri", options.AttachmentDays, "Çözülen veya kapatılan taleplerin dosyaları bu süre sonunda silinir; talebin kendisi kalır."),
        new RetentionRule("Denetim kaydı IP adresleri", options.AuditIpAddressDays, "Kayıt kalır, IP adresi silinir."),
        new RetentionRule("Denetim kaydı", options.AuditLogDays, "Kayıt tamamen silinir."),
        new RetentionRule("Zimmet formları", options.AssignmentFormDays, "İadeden bu süre sonra formdaki ad, departman, açıklama ve imzalar silinir; cihaz bilgileri kalır.")
    };

    public async Task<RetentionCounts> CountDueAsync(DateTime nowUtc) => new(
        await CountAsync(Notifications(nowUtc)),
        await CountAsync(Attachments(nowUtc)),
        await CountAsync(AuditIpAddresses(nowUtc)),
        await CountAsync(AuditLogs(nowUtc)),
        await CountAsync(AssignmentForms(nowUtc)));

    // "by" is the IT staff member who started the run, or null for the daily run
    public async Task<RetentionCounts> RunAsync(DateTime nowUtc, ClaimsPrincipal? by = null, string? ipAddress = null)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var notifications = Notifications(nowUtc) is { } dueNotifications ? await dueNotifications.ExecuteDeleteAsync() : 0;

        // Files go after the rows are saved, so a failed save never leaves rows pointing at missing files
        var attachments = Attachments(nowUtc) is { } dueAttachments ? await dueAttachments.ToListAsync() : new List<TicketAttachment>();
        _context.TicketAttachments.RemoveRange(attachments);
        await _context.SaveChangesAsync();

        // Whole entries first, so the IP rule does not touch rows that are about to go
        var auditLogs = AuditLogs(nowUtc) is { } dueLogs ? await dueLogs.ExecuteDeleteAsync() : 0;
        var auditIps = AuditIpAddresses(nowUtc) is { } dueIps
            ? await dueIps.ExecuteUpdateAsync(s => s.SetProperty(a => a.IpAddress, (string?)null))
            : 0;

        var forms = AssignmentForms(nowUtc) is { } dueForms
            ? await dueForms.ExecuteUpdateAsync(s => s
                .SetProperty(f => f.EmployeeName, PersonalDataAnonymizer.AnonymousName)
                .SetProperty(f => f.Department, (string?)null)
                .SetProperty(f => f.IssuedByName, f => f.IssuedByName == null ? null : PersonalDataAnonymizer.AnonymousName)
                .SetProperty(f => f.ReceivedByName, f => f.ReceivedByName == null ? null : PersonalDataAnonymizer.AnonymousName)
                .SetProperty(f => f.ReturnNote, (string?)null)
                .SetProperty(f => f.AcceptanceSignature, (byte[]?)null)
                .SetProperty(f => f.ReturnSignature, (byte[]?)null)
                .SetProperty(f => f.ReceiverSignature, (byte[]?)null))
            : 0;

        var counts = new RetentionCounts(notifications, attachments.Count, auditIps, auditLogs, forms);
        if (counts.Total > 0)
        {
            _context.AuditLogs.Add(Summary(counts, nowUtc, by, ipAddress));
            await _context.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        foreach (var attachment in attachments)
        {
            _storage.Delete(attachment);
        }
        return counts;
    }

    // Each query is null when its rule is turned off
    private IQueryable<Notification>? Notifications(DateTime now) => _options.NotificationDays <= 0 ? null
        : _context.Notifications.Where(n => n.CreatedAt < now.AddDays(-_options.NotificationDays));

    private IQueryable<TicketAttachment>? Attachments(DateTime now) => _options.AttachmentDays <= 0 ? null
        : _context.TicketAttachments.Where(a => (a.Ticket!.Status == TicketStatus.Resolved || a.Ticket.Status == TicketStatus.Closed)
            && (a.Ticket.ResolvedAt ?? a.Ticket.UpdatedAt ?? a.Ticket.CreatedAt) < now.AddDays(-_options.AttachmentDays));

    private IQueryable<AuditLog>? AuditIpAddresses(DateTime now) => _options.AuditIpAddressDays <= 0 ? null
        : _context.AuditLogs.Where(a => a.IpAddress != null && a.CreatedAt < now.AddDays(-_options.AuditIpAddressDays));

    private IQueryable<AuditLog>? AuditLogs(DateTime now) => _options.AuditLogDays <= 0 ? null
        : _context.AuditLogs.Where(a => a.CreatedAt < now.AddDays(-_options.AuditLogDays));

    // Forms already erased are skipped, so they are not counted again on the next run
    private IQueryable<AssignmentForm>? AssignmentForms(DateTime now) => _options.AssignmentFormDays <= 0 ? null
        : _context.AssignmentForms.Where(f => f.ReturnedAt != null && f.ReturnedAt < now.AddDays(-_options.AssignmentFormDays)
            && (f.EmployeeName != PersonalDataAnonymizer.AnonymousName || f.AcceptanceSignature != null
                || f.ReturnSignature != null || f.ReceiverSignature != null));

    private static async Task<int> CountAsync<T>(IQueryable<T>? query) => query == null ? 0 : await query.CountAsync();

    private static AuditLog Summary(RetentionCounts counts, DateTime nowUtc, ClaimsPrincipal? by, string? ipAddress)
    {
        var changes = new List<AuditChange>();
        void Add(string field, int count, string what)
        {
            if (count > 0)
                changes.Add(new AuditChange(field, null, $"{count} {what}"));
        }
        Add("Bildirimler", counts.Notifications, "silindi");
        Add("Talep ekleri", counts.Attachments, "dosya silindi");
        Add("Denetim kaydı IP adresleri", counts.AuditIpAddresses, "kayıttan silindi");
        Add("Denetim kaydı", counts.AuditLogs, "kayıt silindi");
        Add("Zimmet formları", counts.AssignmentForms, "formdaki kişisel bilgiler silindi");

        return new AuditLog
        {
            CreatedAt = nowUtc,
            UserId = by?.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = by?.Identity?.Name,
            IpAddress = ipAddress,
            Action = AuditAction.Deleted,
            EntityType = AuditEntityType,
            EntityLabel = by == null ? "Günlük saklama süresi kontrolü" : "Saklama süreleri elle uygulandı",
            Changes = JsonSerializer.Serialize(changes)
        };
    }
}
