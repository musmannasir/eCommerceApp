using ECommerceApp.Application.Notifications;
using ECommerceApp.Application.Notifications.Models;

namespace ECommerceApp.Infrastructure.Tests.TestSupport;

/// <summary>In-memory stand-in for <see cref="IEmailNotificationService"/>, so OutboxProcessor tests can isolate its own Pending/Processed/Failed bookkeeping from template rendering/sending.</summary>
public sealed class FakeEmailNotificationService : IEmailNotificationService
{
    public List<(string ToEmail, string ResetLink)> PasswordResetEmailsSent { get; } = [];
    public List<(string ToEmail, OrderConfirmationEmailModel Model)> OrderConfirmationEmailsSent { get; } = [];

    /// <summary>When set, every call throws this instead of recording the send - simulates a rendering/delivery failure.</summary>
    public Exception? ThrowOnSend { get; set; }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        PasswordResetEmailsSent.Add((toEmail, resetLink));
        return Task.CompletedTask;
    }

    public Task SendOrderConfirmationEmailAsync(string toEmail, OrderConfirmationEmailModel model, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        OrderConfirmationEmailsSent.Add((toEmail, model));
        return Task.CompletedTask;
    }
}
