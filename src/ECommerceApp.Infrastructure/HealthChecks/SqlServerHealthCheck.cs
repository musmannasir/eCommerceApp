using ECommerceApp.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ECommerceApp.Infrastructure.HealthChecks;

/// <summary>
/// Reports healthy only if the configured SQL Server database can actually be
/// reached. Used for the <c>/health/ready</c> endpoint; deliberately separate
/// from liveness so a slow/unavailable DB does not make the process look dead.
/// Milestone 17.3 added a bounded timeout - previously a struggling-but-not-
/// fully-down SQL Server (e.g. under heavy load, or mid-failover) could make
/// <c>CanConnectAsync</c> hang for however long the underlying driver takes to
/// give up, which defeats the point of a *readiness* probe an orchestrator
/// polls to decide whether to route traffic here: a slow answer is exactly as
/// unhelpful as no answer. This now fails fast and reports unhealthy instead.
/// </summary>
public sealed class SqlServerHealthCheck : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly ApplicationDbContext _dbContext;

    public SqlServerHealthCheck(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(linkedCts.Token);
            return canConnect
                ? HealthCheckResult.Healthy("SQL Server is reachable.")
                : HealthCheckResult.Unhealthy("SQL Server did not respond.");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"SQL Server did not respond within {Timeout.TotalSeconds}s.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server health check threw an exception.", ex);
        }
    }
}
