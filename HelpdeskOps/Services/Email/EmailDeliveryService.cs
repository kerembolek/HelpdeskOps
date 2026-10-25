using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HelpdeskOps.Services.Email;

// Sends queued e-mails one by one; a failed message is logged and skipped so the rest still go out
public class EmailDeliveryService : BackgroundService
{
    private readonly EmailQueue _queue;
    private readonly EmailOptions _options;
    private readonly string _pickupDirectory;
    private readonly ILogger<EmailDeliveryService> _logger;

    public EmailDeliveryService(EmailQueue queue, IOptions<EmailOptions> options, IWebHostEnvironment environment,
        ILogger<EmailDeliveryService> logger)
    {
        _queue = queue;
        _options = options.Value;
        _pickupDirectory = Path.Combine(environment.ContentRootPath, _options.PickupDirectory);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DeliverAsync(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not send the e-mail \"{Subject}\" to {To}", message.Subject, message.To);
            }
        }
    }

    public async Task DeliverAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = BuildMessage(_options, message);

        if (_options.Mode == EmailMode.Pickup)
        {
            Directory.CreateDirectory(_pickupDirectory);
            // Written under a temporary name first, so nothing ever sees a half-written .eml file
            var file = Path.Combine(_pickupDirectory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.eml");
            await mime.WriteToAsync(file + ".tmp", cancellationToken);
            File.Move(file + ".tmp", file);
            return;
        }

        if (_options.Mode == EmailMode.Smtp)
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port,
                _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);
            if (!string.IsNullOrEmpty(_options.UserName))
            {
                await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
            }
            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }

    public static MimeMessage BuildMessage(EmailOptions options, EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();
        return mime;
    }
}
