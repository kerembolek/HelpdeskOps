namespace HelpdeskOps.Services.Kvkk;

// Applies the retention periods a minute after startup and then once a day
public class RetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<RetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The read-only demo keeps its sample data as it is
        if (DemoReadOnlyMiddleware.IsEnabled(_configuration))
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
            do
            {
                await RunOnceAsync();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // The app is shutting down
        }
    }

    private async Task RunOnceAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var counts = await scope.ServiceProvider.GetRequiredService<RetentionCleaner>().RunAsync(DateTime.UtcNow);
            _logger.LogInformation("Retention run finished: {Counts}", counts);
        }
        catch (Exception ex)
        {
            // A failed run is retried the next day; the app keeps working
            _logger.LogError(ex, "Retention run failed");
        }
    }
}
