using ECommerceApp.Application.Notifications;

namespace ECommerceApp.Infrastructure.Tests.TestSupport;

/// <summary>In-memory stand-in for <see cref="IOutboxProcessor"/>, so background-service tests can observe how often/whether it was invoked without a real database.</summary>
public sealed class FakeOutboxProcessor : IOutboxProcessor
{
    private int _callCount;

    public int CallCount => _callCount;

    /// <summary>When true, the first call throws instead of completing - simulates a processing-pass failure.</summary>
    public bool ThrowOnFirstCall { get; set; }

    public Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var count = Interlocked.Increment(ref _callCount);
        if (ThrowOnFirstCall && count == 1)
        {
            throw new InvalidOperationException("Simulated processing failure.");
        }

        return Task.CompletedTask;
    }
}
