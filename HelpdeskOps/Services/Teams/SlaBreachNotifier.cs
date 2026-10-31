using HelpdeskOps.Data;
using HelpdeskOps.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Services.Teams;

// Finds active tickets that passed their SLA target and puts one card for each on the Teams queue.
public class SlaBreachNotifier
{
    private const int MaxPerRun = 25;

    // Long enough to cover a weekend with the app down; older breaches are stale news, and without this limit
    // the first run after an upgrade would flood the channel with every ticket ever left overdue
    private static readonly TimeSpan OldestBreach = TimeSpan.FromDays(3);

    private readonly ApplicationDbContext _context;
    private readonly TeamsQueue _queue;

    public SlaBreachNotifier(ApplicationDbContext context, TeamsQueue queue)
    {
        _context = context;
        _queue = queue;
    }

    public async Task<int> RunAsync(string publicUrl, DateTime utcNow, CancellationToken cancellationToken)
    {
        var oldest = utcNow - OldestBreach;
        var tickets = await _context.Tickets.AsNoTracking()
            .Where(TicketSla.IsOverdue(utcNow))
            .Where(t => t.SlaBreachNotifiedAt == null && t.SlaDueAt >= oldest)
            .OrderBy(t => t.SlaDueAt)
            .Take(MaxPerRun)
            .ToListAsync(cancellationToken);

        foreach (var ticket in tickets)
        {
            // Written directly: this is bookkeeping, not a change to the ticket, so it stays out of the audit log.
            // Marked before posting, because a lost alert costs less than the same card arriving every few minutes.
            await _context.Tickets.Where(t => t.Id == ticket.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.SlaBreachNotifiedAt, utcNow), cancellationToken);
            _queue.Enqueue(new TeamsMessage(ticket.Id, TeamsCard.BuildSlaBreach(ticket, $"{publicUrl.TrimEnd('/')}/Tickets/Details/{ticket.Id}")));
        }

        return tickets.Count;
    }
}
