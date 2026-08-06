namespace ECommerceApp.Application.Common.Options;

/// <summary>Binds the "Outbox" configuration section.</summary>
public class OutboxOptions
{
    public int PollingIntervalSeconds { get; set; } = 30;
}
