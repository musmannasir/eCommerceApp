using System.Text.Json;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Notifications;
using ECommerceApp.Application.Notifications.Models;
using ECommerceApp.Domain.Notifications;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Notifications;

/// <summary>
/// Milestone 15.2 - see <see cref="IOutboxProcessor"/> for why this isn't a
/// background job yet. A per-message failure (a bad payload, the renderer
/// or sender throwing) is caught individually and recorded on that row
/// alone - never allowed to fail the batch or bubble up to whatever request
/// called <see cref="ProcessPendingAsync"/>, since that request's own
/// business transaction already committed successfully by the time this
/// runs.
/// </summary>
public sealed class OutboxProcessor : IOutboxProcessor
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly IClock _clock;

    public OutboxProcessor(ApplicationDbContext dbContext, IEmailNotificationService emailNotificationService, IClock clock)
    {
        _dbContext = dbContext;
        _emailNotificationService = emailNotificationService;
        _clock = clock;
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            try
            {
                await DispatchAsync(message, cancellationToken);
                message.Status = OutboxMessageStatus.Processed;
                message.ProcessedAtUtc = _clock.UtcNow;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message;
                if (message.Attempts >= MaxAttempts)
                {
                    message.Status = OutboxMessageStatus.Failed;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken) => message.Type switch
    {
        OutboxMessageType.PasswordResetEmail => DispatchPasswordResetAsync(message, cancellationToken),
        OutboxMessageType.OrderConfirmationEmail => DispatchOrderConfirmationAsync(message, cancellationToken),
        _ => throw new InvalidOperationException($"Unknown outbox message type: {message.Type}"),
    };

    private Task DispatchPasswordResetAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PasswordResetEmailOutboxPayload>(message.PayloadJson)
            ?? throw new InvalidOperationException($"Outbox message {message.Id} has an unreadable payload.");
        return _emailNotificationService.SendPasswordResetEmailAsync(payload.ToEmail, payload.ResetLink, cancellationToken);
    }

    private Task DispatchOrderConfirmationAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<OrderConfirmationEmailOutboxPayload>(message.PayloadJson)
            ?? throw new InvalidOperationException($"Outbox message {message.Id} has an unreadable payload.");
        return _emailNotificationService.SendOrderConfirmationEmailAsync(payload.ToEmail, payload.Model, cancellationToken);
    }
}
