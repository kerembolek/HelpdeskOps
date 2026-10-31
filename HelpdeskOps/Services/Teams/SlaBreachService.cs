using HelpdeskOps.Services.Email;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.Teams;

// Looks for newly overdue tickets at a fixed interval. It runs outside any web request, so the card's link needs the
// "Email:PublicUrl" setting; without it, or without the Teams webhook, this stays off.
public class SlaBreachService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly TeamsOptions _teamsOptions;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<SlaBreachService> _logger;

    public SlaBreachService(IServiceScopeFactory scopeFactory, IConfiguration configuration, IOptions<TeamsOptions> teamsOptions,
        IOptions<EmailOptions> emailOptions, ILogger<SlaBreachService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _teamsOptions = teamsOptions.Value;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_teamsOptions.IsEnabled || string.IsNullOrWhiteSpace(_emailOptions.PublicUrl) || DemoReadOnlyMiddleware.IsEnabled(_configuration))
        {
            return;
        }

        try
        {
            using var timer = new PeriodicTimer(Interval);
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
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SlaBreachNotifier>()
                .RunAsync(_emailOptions.PublicUrl!, DateTime.UtcNow, stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Could not check for overdue tickets");
        }
    }
}
