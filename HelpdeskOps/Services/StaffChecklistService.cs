using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services.Kvkk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services;

// Reads the automatic checks of an onboarding or offboarding checklist from the rest of the system, and completes it
public class StaffChecklistService
{
    private readonly ApplicationDbContext _context;
    private readonly KvkkOptions _kvkk;

    public StaffChecklistService(ApplicationDbContext context, IOptions<KvkkOptions> kvkk)
    {
        _context = context;
        _kvkk = kvkk.Value;
    }

    public async Task<ChecklistFacts> FactsAsync(StaffChecklist checklist)
    {
        var userId = checklist.UserId;
        var noticeVersion = await _context.Users.Where(u => u.Id == userId).Select(u => u.PrivacyNoticeVersion).FirstOrDefaultAsync();

        return new ChecklistFacts(
            HasRole: await _context.UserRoles.AnyAsync(r => r.UserId == userId),
            ReadPrivacyNotice: noticeVersion == _kvkk.NoticeVersion,
            AssignedDevices: await _context.Devices.CountAsync(d => d.AssignedUserId == userId),
            UnsignedForms: await _context.AssignmentForms.CountAsync(f => f.UserId == userId && f.ReturnedAt == null && f.AcceptedAt == null),
            OpenForms: await _context.AssignmentForms.CountAsync(f => f.UserId == userId && f.ReturnedAt == null),
            ActiveAssignedTickets: await _context.Tickets.CountAsync(t => t.AssignedToId == userId
                && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)),
            // Only returns made while this checklist was open belong to this offboarding
            ReturnsWithoutSignature: await _context.AssignmentForms.CountAsync(f => f.UserId == userId
                && f.ReturnedAt >= checklist.CreatedAt && f.ReceiverSignature == null),
            AssignedLicenses: await _context.LicenseAssignments.CountAsync(a => a.UserId == userId));
    }

    public async Task<IReadOnlyList<ChecklistCheck>> ChecksAsync(StaffChecklist checklist) =>
        StaffChecklistRules.Checks(checklist.Type, await FactsAsync(checklist));

    // Expects the checklist with its items. Returns why it cannot be completed, or null once it is.
    // Completing an offboarding closes the account: sign-in is locked for good and open sessions stop working.
    public async Task<string?> CompleteAsync(StaffChecklist checklist, string currentUserId)
    {
        if (checklist.Type == ChecklistType.Offboarding && checklist.UserId == currentUserId)
            return "Kendi hesabınızı kapatamazsınız.";

        var blocker = StaffChecklistRules.CompletionBlocker(checklist, await ChecksAsync(checklist));
        if (blocker != null)
            return blocker;

        checklist.CompletedAt = DateTime.UtcNow;
        checklist.CompletedById = currentUserId;

        if (checklist.Type == ChecklistType.Offboarding)
        {
            var user = await _context.Users.SingleAsync(u => u.Id == checklist.UserId);
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        await _context.SaveChangesAsync();
        return null;
    }
}
