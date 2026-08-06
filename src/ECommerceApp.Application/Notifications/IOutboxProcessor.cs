namespace ECommerceApp.Application.Notifications;

/// <summary>
/// Milestone 15.2 - dispatches pending <see cref="ECommerceApp.Domain.Notifications.OutboxMessage"/>
/// rows through <see cref="IEmailNotificationService"/>. Not a background
/// job (Milestone 15.3 is that) - callers invoke this explicitly right
/// after the request that just enqueued something, so delivery still
/// happens promptly today; it also opportunistically retries any other
/// still-pending rows left over from an earlier failed attempt.
/// </summary>
public interface IOutboxProcessor
{
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
}
