using ECommerceApp.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ECommerceApp.Infrastructure.HealthChecks;

/// <summary>
/// Reports healthy only if the configured SQL Server database can actually be
/// reached. Used for the <c>/health/ready</c> endpoint; deliberately separate
/// from liveness so a slow/unavailable DB does not make the process look dead.
/// </summary>
public sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;

    public SqlServerHealthCheck(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("SQL Server is reachable.")
                : HealthCheckResult.Unhealthy("SQL Server did not respond.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server health check threw an exception.", ex);
        }
    }
}
