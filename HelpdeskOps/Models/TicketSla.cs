using System.Linq.Expressions;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models;

public enum SlaState
{
    OnTrack,
    DueSoon,
    Overdue,
    Met,
    Missed
}

// Target resolution times per priority. Critical tickets count around the clock, because a stopped office does
// not wait for the morning; the others count working hours only (BusinessCalendar). The target moment is worked
// out when the ticket is saved (SlaDueInterceptor) and stored in Ticket.SlaDueAt, so lists can filter on it in SQL.
public static class TicketSla
{
    public static int TargetHours(TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => 4,
        TicketPriority.High => 8,
        TicketPriority.Medium => 24,
        _ => 72
    };

    public static bool CountsWorkingHours(TicketPriority priority) => priority != TicketPriority.Critical;

    // "4 saat (7/24)" or "8 mesai saati"
    public static string TargetText(TicketPriority priority) =>
        CountsWorkingHours(priority) ? $"{TargetHours(priority)} mesai saati" : $"{TargetHours(priority)} saat (7/24)";

    // An active ticket is "due soon" once less than a quarter of its target time is left
    private const double DueSoonShare = 0.25;

    public static DateTime CalculateDueAt(DateTime createdAt, TicketPriority priority, BusinessCalendar calendar) =>
        CountsWorkingHours(priority)
            ? calendar.AddWorkingHours(createdAt, TargetHours(priority))
            : createdAt.AddHours(TargetHours(priority));

    // Stored tickets always have SlaDueAt; the fallback covers one that has not been saved yet
    public static DateTime DueAt(Ticket ticket) =>
        ticket.SlaDueAt ?? CalculateDueAt(ticket.CreatedAt, ticket.Priority, BusinessCalendar.Default);

    public static SlaState StateOf(Ticket ticket, DateTime utcNow)
    {
        var dueAt = DueAt(ticket);
        if (ticket.ResolvedAt is DateTime resolvedAt)
            return resolvedAt <= dueAt ? SlaState.Met : SlaState.Missed;
        if (utcNow > dueAt)
            return SlaState.Overdue;
        // Measured on the clock: a weekend in between makes the whole span longer, so the warning comes in proportion
        return dueAt - utcNow < (dueAt - ticket.CreatedAt) * DueSoonShare
            ? SlaState.DueSoon
            : SlaState.OnTrack;
    }

    // Active tickets past their target, as a filter EF Core can translate to SQL
    public static Expression<Func<Ticket, bool>> IsOverdue(DateTime utcNow) =>
        t => (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress) && t.SlaDueAt < utcNow;

    // Resolved tickets that were resolved within their target, for the compliance rate
    public static Expression<Func<Ticket, bool>> MetTarget() =>
        t => t.ResolvedAt != null && t.ResolvedAt <= t.SlaDueAt;
}
