using HelpdeskOps.Data;
using HelpdeskOps.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services;

// Works out a ticket's SLA target whenever a ticket is added or its priority changes, so no controller, seeder, or
// importer has to remember to. Registered before the audit interceptor, so the audit log sees the new target too.
public class SlaDueInterceptor : SaveChangesInterceptor
{
    private readonly BusinessCalendar _calendar;

    public SlaDueInterceptor(IOptions<SlaOptions> options)
    {
        _calendar = new BusinessCalendar(options.Value);
    }

    public BusinessCalendar Calendar => _calendar;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context == null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<Ticket>())
        {
            var needsTarget = entry.State == EntityState.Added
                || (entry.State == EntityState.Modified && entry.Property(t => t.Priority).IsModified
                    && !Equals(entry.Property(t => t.Priority).OriginalValue, entry.Property(t => t.Priority).CurrentValue));
            if (needsTarget)
            {
                entry.Entity.SlaDueAt = TicketSla.CalculateDueAt(entry.Entity.CreatedAt, entry.Entity.Priority, _calendar);
                // A new target can be missed again, and that is a new breach
                entry.Entity.SlaBreachNotifiedAt = null;
            }
        }
    }

    // Tickets saved before targets were stored get one at startup; after the first run there is nothing left to do
    public async Task<int> BackfillAsync(ApplicationDbContext context)
    {
        var tickets = await context.Tickets.Where(t => t.SlaDueAt == null).ToListAsync();
        foreach (var ticket in tickets)
        {
            ticket.SlaDueAt = TicketSla.CalculateDueAt(ticket.CreatedAt, ticket.Priority, _calendar);
        }
        await context.SaveChangesAsync();
        return tickets.Count;
    }
}
