using System.Text;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.Teams;

// Posts queued cards one by one; a failed card is logged and skipped so the rest still go out.
// The ticket itself is safe in the database and in the app's own lists, so a lost card costs only the alert.
public class TeamsDeliveryService : BackgroundService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly TeamsQueue _queue;
    private readonly TeamsOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TeamsDeliveryService> _logger;

    public TeamsDeliveryService(TeamsQueue queue, IOptions<TeamsOptions> options, IHttpClientFactory httpClientFactory,
        ILogger<TeamsDeliveryService> logger)
    {
        _queue = queue;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
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
            // A request timeout is also a cancellation; only the app shutting down may end this loop
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Could not post ticket {TicketId} to Teams", message.TicketId);
            }
        }
    }

    public async Task DeliverAsync(TeamsMessage message, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = RequestTimeout;
        using var content = new StringContent(message.Json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(_options.WebhookUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
