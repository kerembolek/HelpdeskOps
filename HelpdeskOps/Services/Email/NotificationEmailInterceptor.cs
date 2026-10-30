using System.Runtime.CompilerServices;
using HelpdeskOps.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.Email;

// Mails every in-app notification once it is saved, so no controller has to remember to send one.
// Outside a web request the links need the "PublicUrl" setting; without it (the demo data seeder at startup) nothing is mailed.
public class NotificationEmailInterceptor : SaveChangesInterceptor
{
    private readonly EmailQueue _queue;
    private readonly EmailOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Notifications added in the current save, collected before EF Core marks them unchanged. Kept per context: with
    // several interceptors EF Core runs each step in its own async call, so an AsyncLocal is gone by the next step.
    private readonly ConditionalWeakTable<DbContext, List<Notification>> _pending = new();

    public NotificationEmailInterceptor(EmailQueue queue, IOptions<EmailOptions> options, IHttpContextAccessor httpContextAccessor)
    {
        _queue = queue;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_options.IsEnabled && eventData.Context != null)
        {
            var added = eventData.Context.ChangeTracker.Entries<Notification>()
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

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        List<Notification>? notifications = null;
        if (eventData.Context != null && _pending.TryGetValue(eventData.Context, out notifications))
        {
            _pending.Remove(eventData.Context);
        }

        var request = _httpContextAccessor.HttpContext?.Request;
        var baseUrl = request != null ? $"{request.Scheme}://{request.Host}{request.PathBase}" : _options.PublicUrl?.TrimEnd('/');
        if (notifications is { Count: > 0 } && !string.IsNullOrEmpty(baseUrl) && eventData.Context != null)
        {
            var userIds = notifications.Select(n => n.UserId).Distinct().ToList();
            var emails = await eventData.Context.Set<ApplicationUser>()
                .Where(u => userIds.Contains(u.Id) && u.Email != null && u.AnonymizedAt == null)
                .ToDictionaryAsync(u => u.Id, u => u.Email!, cancellationToken);

            foreach (var notification in notifications)
            {
                if (!emails.TryGetValue(notification.UserId, out var email))
                {
                    continue;
                }

                // Only local paths are stored, so the link always stays on this site
                var link = notification.Url.StartsWith('/') ? baseUrl + notification.Url : baseUrl + "/Notifications";
                _queue.Enqueue(new EmailMessage(email, "Yeni bildirim: " + Shorten(notification.Message),
                    EmailTemplate.Wrap("Yeni bildiriminiz var",
                        EmailTemplate.MessageWithButton(notification.Message, "HelpdeskOps'ta Görüntüle", link))));
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static string Shorten(string text) => text.Length > 70 ? text[..70] + "…" : text;
}
