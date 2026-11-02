using HelpdeskOps.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HelpdeskOps.Services;

// The site is only useful with its database, so /health reports whether it can be reached
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;

    public DatabaseHealthCheck(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await _context.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
