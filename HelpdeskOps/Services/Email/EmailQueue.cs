using System.Threading.Channels;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.Email;

public record EmailMessage(string To, string Subject, string HtmlBody);

// Requests only put messages on this queue; EmailDeliveryService sends them in the background,
// so a slow or failing mail server never slows down or breaks a page
public class EmailQueue
{
    // Bounded so a broken mail server cannot make the queue grow without limit; the oldest waiting message is dropped
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

// The sender ASP.NET Core Identity uses for confirmation and password reset mails
public class QueuedEmailSender : IEmailSender
{
    private readonly EmailQueue _queue;
    private readonly EmailOptions _options;

    public QueuedEmailSender(EmailQueue queue, IOptions<EmailOptions> options)
    {
        _queue = queue;
        _options = options.Value;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (_options.IsEnabled)
        {
            _queue.Enqueue(new EmailMessage(email, subject, EmailTemplate.Wrap(subject, htmlMessage)));
        }
        return Task.CompletedTask;
    }
}
