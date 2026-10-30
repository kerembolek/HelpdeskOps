using System.Text.Json;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Services.Kvkk;

// Erases a former employee's identity for a KVKK deletion request. Their tickets, comments, and history are company
// records, so the account is not deleted: it becomes "Silinmiş Kullanıcı" with a made-up address, cannot sign in,
// and the records keep pointing at it. Zimmet forms are left as they are, because they prove a handover and are kept
// for their retention period; the retention rules anonymize them when that period ends.
public class PersonalDataAnonymizer
{
    public const string AnonymousFirstName = "Silinmiş";
    public const string AnonymousLastName = "Kullanıcı";
    public const string AnonymousName = AnonymousFirstName + " " + AnonymousLastName;

    private readonly ApplicationDbContext _context;

    public PersonalDataAnonymizer(ApplicationDbContext context)
    {
        _context = context;
    }

    // Returns why the person cannot be anonymized yet, or null when they can
    public async Task<string?> CheckAsync(ApplicationUser user, string currentUserId)
    {
        if (user.Id == currentUserId)
            return "Kendi hesabınızı anonimleştiremezsiniz.";
        if (user.AnonymizedAt != null)
            return "Bu kullanıcı zaten anonimleştirilmiş.";

        var devices = await _context.Devices.CountAsync(d => d.AssignedUserId == user.Id);
        if (devices > 0)
            return $"Önce üzerindeki {devices} cihazı geri alın.";

        var openForms = await _context.AssignmentForms.CountAsync(f => f.UserId == user.Id && f.ReturnedAt == null);
        if (openForms > 0)
            return $"Önce iade edilmemiş {openForms} zimmet formunu kapatın.";

        var activeTickets = await _context.Tickets.CountAsync(t => t.AssignedToId == user.Id
            && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress));
        if (activeTickets > 0)
            return $"Önce üzerindeki {activeTickets} aktif talebi başka birine atayın.";

        return null;
    }

    // The caller checks CheckAsync first. Everything happens in one transaction.
    public async Task AnonymizeAsync(ApplicationUser user)
    {
        var oldName = $"{user.FirstName} {user.LastName}";
        var oldEmail = user.Email;
        var newEmail = $"silinmis-{Guid.NewGuid():N}"[..17] + "@anonim.invalid";

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Old audit entries first, so the entries this save writes are not touched
        await ScrubAuditLogAsync(user.Id, oldName, oldEmail, newEmail);

        // History shows people by name; only exact matches are replaced, so free text in tickets and comments stays as written
        await _context.TicketActivities.Where(a => a.OldValue == oldName).ExecuteUpdateAsync(s => s.SetProperty(a => a.OldValue, AnonymousName));
        await _context.TicketActivities.Where(a => a.NewValue == oldName).ExecuteUpdateAsync(s => s.SetProperty(a => a.NewValue, AnonymousName));

        user.FirstName = AnonymousFirstName;
        user.LastName = AnonymousLastName;
        user.UserName = newEmail;
        user.NormalizedUserName = newEmail.ToUpperInvariant();
        user.Email = newEmail;
        user.NormalizedEmail = newEmail.ToUpperInvariant();
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.Department = null;
        user.DirectoryId = null;

        // No password, no second factor, locked for good, and every existing sign-in cookie stops working
        user.PasswordHash = null;
        user.TwoFactorEnabled = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.AccessFailedCount = 0;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AnonymizedAt = DateTime.UtcNow;

        _context.UserRoles.RemoveRange(_context.UserRoles.Where(r => r.UserId == user.Id));
        _context.UserClaims.RemoveRange(_context.UserClaims.Where(c => c.UserId == user.Id));
        _context.UserLogins.RemoveRange(_context.UserLogins.Where(l => l.UserId == user.Id));
        // Authenticator key and recovery codes
        _context.UserTokens.RemoveRange(_context.UserTokens.Where(t => t.UserId == user.Id));
        _context.Notifications.RemoveRange(_context.Notifications.Where(n => n.UserId == user.Id));
        _context.InboundEmails.RemoveRange(_context.InboundEmails.Where(e => e.UserId == user.Id));

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    // The audit log keeps every change, but not who the person was: their own entries lose the IP address, changes to
    // their account lose the values, and their name and e-mail are replaced wherever they appear. Someone else with
    // exactly the same name would be replaced too, which is the safer mistake.
    private async Task ScrubAuditLogAsync(string userId, string oldName, string? oldEmail, string newEmail)
    {
        await _context.AuditLogs.Where(a => a.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.UserName, newEmail).SetProperty(a => a.IpAddress, (string?)null));

        await _context.AuditLogs.Where(a => (a.EntityType == "ApplicationUser" || a.EntityType == "UserRole") && a.EntityId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.EntityLabel, newEmail)
                .SetProperty(a => a.Changes, a => a.EntityType == "ApplicationUser" ? null : a.Changes));

        // Changes are stored as JSON, where letters such as "ü" are escaped
        var replacements = new List<(string Old, string New)> { (oldName, AnonymousName), (JsonText(oldName), JsonText(AnonymousName)) };
        if (!string.IsNullOrEmpty(oldEmail))
        {
            replacements.Add((oldEmail, newEmail));
        }

        foreach (var (oldText, newText) in replacements.Distinct())
        {
            await _context.AuditLogs.Where(a => a.EntityLabel != null && a.EntityLabel.Contains(oldText))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.EntityLabel, a => a.EntityLabel!.Replace(oldText, newText)));
            await _context.AuditLogs.Where(a => a.Changes != null && a.Changes.Contains(oldText))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Changes, a => a.Changes!.Replace(oldText, newText)));
        }
    }

    private static string JsonText(string text) => JsonSerializer.Serialize(text)[1..^1];
}
