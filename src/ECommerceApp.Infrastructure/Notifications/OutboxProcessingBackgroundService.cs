using ECommerceApp.Application.Common.Options;
using ECommerceApp.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerceApp.Infrastructure.Notifications;

/// <summary>
/// Milestone 15.3 - the safety net Milestone 15.2's own design left for this
/// milestone to build: <see cref="IOutboxProcessor.ProcessPendingAsync"/> is
/// still called synchronously right after a request enqueues something (so
/// delivery stays prompt for the common case), but nothing before this
/// retried a message left <c>Pending</c> by a crashed request or a transient
/// failure unless some other matching request happened to come in later.
/// This runs a sweep immediately on startup (catching anything left over
/// from before a restart), then on <see cref="OutboxOptions.PollingIntervalSeconds"/>.
/// A processing-pass failure is caught here and logged rather than left to
/// propagate - by default an unhandled exception from a <see cref="BackgroundService"/>
/// stops the entire host, the same "don't take the app down" reasoning
/// <c>Program.cs</c> already applies to role/admin seeding failures.
/// </summary>
public sealed class OutboxProcessingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessingBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public OutboxProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollingIntervalSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Background outbox processing pass failed - will retry on the next interval.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
