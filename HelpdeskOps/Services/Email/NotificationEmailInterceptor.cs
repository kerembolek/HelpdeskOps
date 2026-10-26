using HelpdeskOps.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.Email;

// Mails every in-app notification once it is saved, so no controller has to remember to send one.
// Notifications saved outside a web request (the demo data seeder at startup) are not mailed.
public class NotificationEmailInterceptor : SaveChangesInterceptor
{
    private readonly EmailQueue _queue;
    private readonly EmailOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Notifications added in the current save, collected before EF Core marks them unchanged
    private readonly AsyncLocal<List<Notification>?> _pending = new();

    public NotificationEmailInterceptor(EmailQueue queue, IOptions<EmailOptions> options, IHttpContextAccessor httpContextAccessor)
    {
        _queue = queue;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        _pending.Value = _options.IsEnabled && eventData.Context != null
            ? eventData.Context.ChangeTracker.Entries<Notification>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList()
            : null;
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        var notifications = _pending.Value;
        _pending.Value = null;

        var request = _httpContextAccessor.HttpContext?.Request;
        if (notifications is { Count: > 0 } && request != null && eventData.Context != null)
        {
            var userIds = notifications.Select(n => n.UserId).Distinct().ToList();
            var emails = await eventData.Context.Set<ApplicationUser>()
                .Where(u => userIds.Contains(u.Id) && u.Email != null && u.AnonymizedAt == null)
                .ToDictionaryAsync(u => u.Id, u => u.Email!, cancellationToken);

            var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
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
