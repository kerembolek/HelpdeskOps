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

// Target resolution times per priority, counted in calendar hours from ticket creation
public static class TicketSla
{
    public static int TargetHours(TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => 4,
        TicketPriority.High => 8,
        TicketPriority.Medium => 24,
        _ => 72
    };

    // An active ticket is "due soon" once less than a quarter of its target time is left
    private const double DueSoonShare = 0.25;

    public static DateTime DueAt(Ticket ticket) => ticket.CreatedAt.AddHours(TargetHours(ticket.Priority));

    public static SlaState StateOf(Ticket ticket, DateTime utcNow)
    {
        var dueAt = DueAt(ticket);
        if (ticket.ResolvedAt is DateTime resolvedAt)
            return resolvedAt <= dueAt ? SlaState.Met : SlaState.Missed;
        if (utcNow > dueAt)
            return SlaState.Overdue;
        return dueAt - utcNow < TimeSpan.FromHours(TargetHours(ticket.Priority) * DueSoonShare)
            ? SlaState.DueSoon
            : SlaState.OnTrack;
    }

    // Active tickets past their target, as a filter EF Core can translate to SQL
    public static Expression<Func<Ticket, bool>> IsOverdue(DateTime utcNow)
    {
        var critical = utcNow.AddHours(-TargetHours(TicketPriority.Critical));
        var high = utcNow.AddHours(-TargetHours(TicketPriority.High));
        var medium = utcNow.AddHours(-TargetHours(TicketPriority.Medium));
        var low = utcNow.AddHours(-TargetHours(TicketPriority.Low));

        return t => (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)
            && ((t.Priority == TicketPriority.Critical && t.CreatedAt < critical)
                || (t.Priority == TicketPriority.High && t.CreatedAt < high)
                || (t.Priority == TicketPriority.Medium && t.CreatedAt < medium)
                || (t.Priority == TicketPriority.Low && t.CreatedAt < low));
    }

    // Resolved tickets that were resolved within their target, for the compliance rate
    public static Expression<Func<Ticket, bool>> MetTarget() =>
        t => t.ResolvedAt != null
            && ((t.Priority == TicketPriority.Critical && t.ResolvedAt <= t.CreatedAt.AddHours(TargetHours(TicketPriority.Critical)))
                || (t.Priority == TicketPriority.High && t.ResolvedAt <= t.CreatedAt.AddHours(TargetHours(TicketPriority.High)))
                || (t.Priority == TicketPriority.Medium && t.ResolvedAt <= t.CreatedAt.AddHours(TargetHours(TicketPriority.Medium)))
                || (t.Priority == TicketPriority.Low && t.ResolvedAt <= t.CreatedAt.AddHours(TargetHours(TicketPriority.Low))));
}
