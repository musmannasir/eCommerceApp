using ECommerceApp.Application.Common.Options;
using ECommerceApp.Application.Notifications;
using ECommerceApp.Infrastructure.Notifications;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerceApp.Infrastructure.Tests.Notifications;

public class OutboxProcessingBackgroundServiceTests
{
    [Fact]
    public async Task Processes_pending_messages_immediately_on_start()
    {
        var (service, fakeProcessor) = CreateService(pollingIntervalSeconds: 300);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        fakeProcessor.CallCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Processes_again_after_the_configured_interval_elapses()
    {
        var (service, fakeProcessor) = CreateService(pollingIntervalSeconds: 1);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(1300);
        await service.StopAsync(CancellationToken.None);

        // Once immediately on start, once more after the ~1s interval.
        fakeProcessor.CallCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task A_processing_pass_that_throws_does_not_stop_the_background_loop()
    {
        var (service, fakeProcessor) = CreateService(pollingIntervalSeconds: 1, throwOnFirstCall: true);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(1300);
        await service.StopAsync(CancellationToken.None);

        // The first call threw, but the loop kept running and ticked again.
        fakeProcessor.CallCount.Should().BeGreaterThanOrEqualTo(2);
    }

    private static (OutboxProcessingBackgroundService Service, FakeOutboxProcessor Processor) CreateService(
        int pollingIntervalSeconds, bool throwOnFirstCall = false)
    {
        var fakeProcessor = new FakeOutboxProcessor { ThrowOnFirstCall = throwOnFirstCall };

        var services = new ServiceCollection();
        services.AddScoped<IOutboxProcessor>(_ => fakeProcessor);
        var provider = services.BuildServiceProvider();

        var options = Options.Create(new OutboxOptions { PollingIntervalSeconds = pollingIntervalSeconds });
        var service = new OutboxProcessingBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<OutboxProcessingBackgroundService>.Instance);

        return (service, fakeProcessor);
    }
}
