using System.Runtime.CompilerServices;
using HelpdeskOps.Models;
using HelpdeskOps.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.Teams;

// Posts every new ticket to the IT channel once it is saved, whether it came from the site or from the helpdesk mailbox.
// Outside a web request the link needs the "Email:PublicUrl" setting; without it (the demo data seeder at startup) nothing is posted.
public class TicketTeamsInterceptor : SaveChangesInterceptor
{
    private readonly TeamsQueue _queue;
    private readonly TeamsOptions _options;
    private readonly EmailOptions _emailOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Tickets added in the current save, collected before EF Core marks them unchanged and gives them their ids.
    // Kept per context for the same reason as NotificationEmailInterceptor.
    private readonly ConditionalWeakTable<DbContext, List<Ticket>> _pending = new();

    public TicketTeamsInterceptor(TeamsQueue queue, IOptions<TeamsOptions> options, IOptions<EmailOptions> emailOptions,
        IHttpContextAccessor httpContextAccessor)
    {
        _queue = queue;
        _options = options.Value;
        _emailOptions = emailOptions.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_options.IsEnabled && eventData.Context != null)
        {
            var added = eventData.Context.ChangeTracker.Entries<Ticket>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();
            // The audit log's own save runs in between and has nothing to add; it must not wipe this list
            if (added.Count > 0)
            {
                _pending.AddOrUpdate(eventData.Context, added);
            }
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null)
        {
            _pending.Remove(eventData.Context);
        }
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        List<Ticket>? tickets = null;
        if (eventData.Context != null && _pending.TryGetValue(eventData.Context, out tickets))
        {
            _pending.Remove(eventData.Context);
        }

        var request = _httpContextAccessor.HttpContext?.Request;
        var baseUrl = request != null ? $"{request.Scheme}://{request.Host}{request.PathBase}" : _emailOptions.PublicUrl?.TrimEnd('/');
        if (tickets is { Count: > 0 } && !string.IsNullOrEmpty(baseUrl))
        {
            foreach (var ticket in tickets)
            {
                _queue.Enqueue(new TeamsMessage(ticket.Id, TeamsCard.Build(ticket, $"{baseUrl}/Tickets/Details/{ticket.Id}")));
            }
        }

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
