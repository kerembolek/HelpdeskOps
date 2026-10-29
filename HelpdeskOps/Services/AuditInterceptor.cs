using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HelpdeskOps.Services;

// Writes an AuditLog row for every saved change to the records a company needs to account for: who changed what,
// when, and from where. Changes saved outside a web request (the demo data seeder at startup) are not recorded.
public class AuditInterceptor : SaveChangesInterceptor
{
    // Audited record types, keyed by AuditLog.EntityType, with the Turkish names the audit page shows
    public static readonly IReadOnlyDictionary<string, string> EntityNames = new Dictionary<string, string>
    {
        ["Device"] = "Cihaz",
        ["Ticket"] = "Talep",
        ["AssignmentForm"] = "Zimmet Formu",
        ["SolutionArticle"] = "Çözüm Makalesi",
        ["TicketAttachment"] = "Talep Eki",
        ["ApplicationUser"] = "Kullanıcı",
        ["UserRole"] = "Kullanıcı Rolü",
        ["StaffChecklist"] = "Giriş / Çıkış Listesi",
        ["ChecklistItem"] = "Liste Adımı",
        ["SoftwareLicense"] = "Yazılım Lisansı",
        ["LicenseAssignment"] = "Lisans Ataması",
        ["Consumable"] = "Sarf Malzemesi",
        ["ConsumableMovement"] = "Stok Hareketi",
        // Not a record type: the summary RetentionCleaner writes after deleting expired personal data
        ["Retention"] = "Saklama Süresi"
    };

    // Requests without a signed-in user (such as the inventory agent) can name themselves for the audit log here
    public const string ActorNameItem = "AuditActorName";

    // Housekeeping values that change on their own or mean nothing to a reader
    private static readonly HashSet<string> IgnoredProperties = new()
    {
        "Id", "UpdatedAt", "LastUpdated", "ConcurrencyStamp", "SecurityStamp", "NormalizedEmail", "NormalizedUserName",
        "AccessFailedCount", "StoredName", "SolvedCount", "EscalatedCount", "SortOrder",
        // The inventory agent reports every day; only real hardware or address changes are worth a record
        "LastInventoryAt"
    };

    // Recorded as "changed" without any value (security)
    private static readonly HashSet<string> SecretProperties = new() { "PasswordHash" };

    // Foreign keys to users; their values are replaced by the person's name
    private static readonly Dictionary<string, string> UserReferences = new()
    {
        ["AssignedUserId"] = "Zimmetli Kullanıcı",
        ["RequesterId"] = "Talep Eden",
        ["AssignedToId"] = "Atanan Kişi",
        ["IssuedById"] = "Teslim Eden",
        ["ReceivedById"] = "Teslim Alan (BT)",
        ["UserId"] = "Kullanıcı",
        ["UploadedById"] = "Yükleyen",
        ["CreatedById"] = "Açan",
        ["CompletedById"] = "Tamamlayan",
        ["DoneById"] = "İşaretleyen",
        ["AssignedById"] = "Atayan"
    };

    private sealed record PendingAudit(EntityEntry Entry, object Entity, AuditAction Action, string EntityType,
        List<(string Property, object? Old, object? New)> Values);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ConditionalWeakTable<DbContext, List<PendingAudit>> _pending = new();
    private readonly ConditionalWeakTable<DbContext, object> _writingAudit = new();

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        WriteAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await WriteAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context != null)
        {
            _pending.Remove(eventData.Context);
        }
        base.SaveChangesFailed(eventData);
    }

    // Reads old and new values before EF Core accepts the changes and forgets the originals
    private void Collect(DbContext? context)
    {
        if (context == null || _httpContextAccessor.HttpContext == null || _writingAudit.TryGetValue(context, out _))
        {
            return;
        }

        var pending = new List<PendingAudit>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || TypeKey(entry.Entity) is not string type)
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                _ => AuditAction.Updated
            };

            var values = new List<(string, object?, object?)>();
            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                if (IgnoredProperties.Contains(name))
                {
                    continue;
                }

                if (action == AuditAction.Created && property.CurrentValue != null)
                {
                    values.Add((name, null, property.CurrentValue));
                }
                else if (action == AuditAction.Updated && property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                {
                    values.Add((name, property.OriginalValue, property.CurrentValue));
                }
            }

            // Anonymizing a person must not copy their old name and e-mail into the audit log, so only the fact is kept
            if (entry.Entity is ApplicationUser && action == AuditAction.Updated
                && entry.Property(nameof(ApplicationUser.AnonymizedAt)) is { IsModified: true, CurrentValue: not null } anonymized)
            {
                values = new() { (anonymized.Metadata.Name, null, anonymized.CurrentValue) };
            }

            // An update that only touched housekeeping values (a sign-in, a counter) is not worth a record
            if (action == AuditAction.Updated && values.Count == 0)
            {
                continue;
            }

            pending.Add(new PendingAudit(entry, entry.Entity, action, type, values));
        }

        if (pending.Count > 0)
        {
            _pending.AddOrUpdate(context, pending);
        }
    }

    private async Task WriteAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context == null || !_pending.TryGetValue(context, out var pending))
        {
            return;
        }
        _pending.Remove(context);

        var http = _httpContextAccessor.HttpContext;
        var userId = http?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = http?.User.Identity?.Name ?? http?.Items[ActorNameItem] as string;
        var ip = http?.Connection.RemoteIpAddress?.ToString();

        // Names for every user and role the changes point at, in two queries
        var userIds = pending.SelectMany(p => p.Values)
            .Where(v => UserReferences.ContainsKey(v.Property))
            .SelectMany(v => new[] { v.Old as string, v.New as string })
            .Concat(pending.Select(p => p.Entity).OfType<IdentityUserRole<string>>().Select(r => r.UserId))
            .Where(id => id != null).Distinct().ToList();
        var userNames = userIds.Count == 0
            ? new Dictionary<string, string>()
            : await context.Set<ApplicationUser>().Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, cancellationToken);
        var roleIds = pending.Select(p => p.Entity).OfType<IdentityUserRole<string>>().Select(r => r.RoleId).Distinct().ToList();
        var roleNames = roleIds.Count == 0
            ? new Dictionary<string, string>()
            : await context.Set<IdentityRole>().Where(r => roleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => AppRoles.DisplayName(r.Name), cancellationToken);

        var logs = new List<AuditLog>();
        foreach (var item in pending)
        {
            List<AuditChange> changes;
            string? label;
            if (item.Entity is IdentityUserRole<string> userRole)
            {
                var role = roleNames.GetValueOrDefault(userRole.RoleId, userRole.RoleId);
                changes = new() { item.Action == AuditAction.Deleted ? new AuditChange("Rol", role, null) : new AuditChange("Rol", null, role) };
                label = userNames.GetValueOrDefault(userRole.UserId);
            }
            else
            {
                changes = item.Values.Select(v => Describe(item.Entity.GetType(), v.Property, v.Old, v.New, userNames)).ToList();
                label = Label(item.Entity);
            }

            logs.Add(new AuditLog
            {
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                UserName = userName,
                IpAddress = ip,
                Action = item.Action,
                EntityType = item.EntityType,
                EntityId = KeyOf(item.Entry),
                EntityLabel = Truncate(label, 300),
                Changes = changes.Count > 0 ? JsonSerializer.Serialize(changes) : null
            });
        }

        // The audit rows go in a second save; this flag keeps that save from auditing itself
        _writingAudit.AddOrUpdate(context, new object());
        try
        {
            context.Set<AuditLog>().AddRange(logs);
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _writingAudit.Remove(context);
        }
    }

    public static string? TypeKey(object entity) => entity switch
    {
        IdentityUserRole<string> => "UserRole",
        _ when EntityNames.ContainsKey(entity.GetType().Name) => entity.GetType().Name,
        _ => null
    };

    private static string? KeyOf(EntityEntry entry)
    {
        if (entry.Entity is IdentityUserRole<string> userRole)
        {
            return userRole.UserId;
        }
        var key = entry.Metadata.FindPrimaryKey();
        return key == null ? null : string.Join(",", key.Properties.Select(p => entry.Property(p.Name).CurrentValue));
    }

    private static string? Label(object entity) => entity switch
    {
        Device d => d.Hostname,
        Ticket t => $"#{t.Id} {t.Title}",
        AssignmentForm f => $"{f.FormNumber} · {f.Hostname} · {f.EmployeeName}",
        SolutionArticle a => a.Title,
        TicketAttachment a => a.FileName,
        ApplicationUser u => u.Email,
        StaffChecklist c => $"{c.Type.GetDisplayName()} #{c.Id}",
        ChecklistItem i => i.Title,
        SoftwareLicense l => l.Name,
        LicenseAssignment a => $"Lisans #{a.SoftwareLicenseId}",
        Consumable c => c.Name,
        ConsumableMovement m => $"Sarf #{m.ConsumableId}",
        _ => null
    };

    public static AuditChange Describe(Type entityType, string property, object? oldValue, object? newValue,
        IReadOnlyDictionary<string, string> userNames)
    {
        if (SecretProperties.Contains(property))
        {
            return new AuditChange("Şifre", null, "(değiştirildi)");
        }
        if (UserReferences.TryGetValue(property, out var userField))
        {
            return new AuditChange(userField, UserName(oldValue, userNames), UserName(newValue, userNames));
        }

        var field = entityType.GetProperty(property)?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? property;
        return new AuditChange(field, Format(property, oldValue), Format(property, newValue));
    }

    private static string? UserName(object? id, IReadOnlyDictionary<string, string> userNames) =>
        id is string userId ? userNames.GetValueOrDefault(userId, userId) : null;

    public static string? Format(string property, object? value) => value switch
    {
        null => null,
        Enum e => e.GetDisplayName(),
        bool b => b ? "Evet" : "Hayır",
        byte[] => "(imza)",
        // Purchase and warranty dates are calendar dates; everything else is a UTC moment
        DateTime d when property.EndsWith("Date") => d.ToString("dd.MM.yyyy"),
        DateTime d => d.ToTurkeyTime().ToString("dd.MM.yyyy HH:mm"),
        DateTimeOffset o => o.UtcDateTime.ToTurkeyTime().ToString("dd.MM.yyyy HH:mm"),
        _ => Truncate(value.ToString(), 200)
    };

    private static string? Truncate(string? text, int length) =>
        text == null || text.Length <= length ? text : text[..(length - 1)] + "…";
}
