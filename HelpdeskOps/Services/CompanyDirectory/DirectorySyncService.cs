using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.CompanyDirectory;

// Runs the directory sync a minute after startup and then once a day
public class DirectorySyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly DirectoryOptions _options;
    private readonly ILogger<DirectorySyncService> _logger;

    public DirectorySyncService(IServiceScopeFactory scopeFactory, IConfiguration configuration, IOptions<DirectoryOptions> options,
        ILogger<DirectorySyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _options = options.Value;
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
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
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
            var result = await scope.ServiceProvider.GetRequiredService<DirectorySync>().RunAsync(stoppingToken);
            if (result.AbortReason != null)
            {
                _logger.LogError("Directory sync stopped: {Reason}", result.AbortReason);
                return;
            }
            _logger.LogInformation("Directory sync finished: {Created} created, {Updated} updated, {Closed} closed, {Errors} errors",
                result.Created, result.Updated, result.Closed, result.Errors.Count);
            foreach (var error in result.Errors)
            {
                _logger.LogWarning("Directory sync could not apply: {Error}", error);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A failed run is retried the next day; the app keeps working
            _logger.LogError(ex, "Directory sync failed");
        }
    }
}
