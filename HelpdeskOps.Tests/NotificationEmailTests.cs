using HelpdeskOps.Models;
using HelpdeskOps.Services;
using HelpdeskOps.Services.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

// The app runs several interceptors together, which is the case that once lost every notification mail
public class NotificationEmailTests : KvkkTestDatabase
{
    private readonly EmailQueue _queue;

    public NotificationEmailTests() : this(new EmailQueue())
    {
    }

    private NotificationEmailTests(EmailQueue queue) : base(
        new AuditInterceptor(new HttpContextAccessor()),
        new NotificationEmailInterceptor(queue,
            Options.Create(new EmailOptions { Mode = EmailMode.Pickup, PublicUrl = "https://destek.test" }), new HttpContextAccessor()))
    {
        _queue = queue;
    }

    private async Task<List<EmailMessage>> SentMailAsync()
    {
        var sent = new List<EmailMessage>();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        try
        {
            await foreach (var message in _queue.ReadAllAsync(cancel.Token))
            {
                sent.Add(message);
            }
        }
        catch (OperationCanceledException)
        {
        }
        return sent;
    }

    [Fact]
    public async Task SavedNotificationIsMailed()
    {
        Context.Notifications.Add(new Notification { UserId = "emp-1", Message = "#1 \"Yazıcı\": BT ekibi yeni bir yorum yazdı.", Url = "/Tickets/Details/1" });
        await Context.SaveChangesAsync();

        var mail = Assert.Single(await SentMailAsync());
        Assert.Equal("onur@x.com", mail.To);
        Assert.Contains("https://destek.test/Tickets/Details/1", mail.HtmlBody);
    }

    [Fact]
    public async Task EachNotificationIsMailedOnce()
    {
        Context.Notifications.Add(new Notification { UserId = "emp-1", Message = "Bir", Url = "/" });
        await Context.SaveChangesAsync();
        Context.Notifications.Add(new Notification { UserId = "emp-1", Message = "İki", Url = "/" });
        await Context.SaveChangesAsync();

        Assert.Equal(2, (await SentMailAsync()).Count);
    }
}
