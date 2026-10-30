using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.Email;

// Reads the helpdesk mailbox over IMAP at a fixed interval. A mail is marked as read only after it was handled, so a
// crash means it is read again, and InboundEmail keeps that second read from opening a second ticket.
public class InboundEmailService : BackgroundService
{
    // Larger mails are left alone: attachments are limited to 5 MB each anyway
    private const uint MaxMessageSize = 30 * 1024 * 1024;
    private const int MaxMessagesPerRun = 25;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly InboundEmailOptions _options;
    private readonly ILogger<InboundEmailService> _logger;

    public InboundEmailService(IServiceScopeFactory scopeFactory, IConfiguration configuration, IOptions<EmailOptions> options,
        ILogger<InboundEmailService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _options = options.Value.Inbound;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsEnabled || DemoReadOnlyMiddleware.IsEnabled(_configuration))
        {
            return;
        }

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(_options.PollSeconds, 15)));
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // The app is shutting down
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(_options.Host, _options.Port,
                _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None, stoppingToken);
            await client.AuthenticateAsync(_options.UserName, _options.Password, stoppingToken);

            var folder = await client.GetFolderAsync(_options.Folder, stoppingToken);
            await folder.OpenAsync(FolderAccess.ReadWrite, stoppingToken);

            var unread = await folder.SearchAsync(SearchQuery.NotSeen, stoppingToken);
            var sizes = await folder.FetchAsync(unread.Take(MaxMessagesPerRun).ToList(), MessageSummaryItems.UniqueId | MessageSummaryItems.Size, stoppingToken);
            foreach (var summary in sizes)
            {
                await HandleAsync(folder, summary, stoppingToken);
            }

            await client.DisconnectAsync(true, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A mail server problem is retried at the next interval; the app keeps working
            _logger.LogError(ex, "Reading the helpdesk mailbox failed");
        }
    }

    private async Task HandleAsync(IMailFolder folder, IMessageSummary summary, CancellationToken stoppingToken)
    {
        try
        {
            if (summary.Size <= MaxMessageSize)
            {
                using var message = await folder.GetMessageAsync(summary.UniqueId, stoppingToken);
                using var scope = _scopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<InboundEmailProcessor>().ProcessAsync(message, stoppingToken);
                _logger.LogInformation("Helpdesk mail handled: {Result}", result);
            }
            else
            {
                _logger.LogWarning("Helpdesk mail skipped: {Size} bytes is over the limit", summary.Size);
            }

            await folder.AddFlagsAsync(summary.UniqueId, MessageFlags.Seen, true, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Stays unread and is tried again at the next interval; the other mails still go through
            _logger.LogError(ex, "A helpdesk mail could not be handled");
        }
    }
}
