using ECommerceApp.Application.Notifications.Models;
using ECommerceApp.Domain.Notifications;
using ECommerceApp.Infrastructure.Notifications;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Tests.Notifications;

public class OutboxProcessorTests : IDisposable
{
    private readonly TestDbContext _dbContext;
    private readonly FakeClock _clock = new();
    private readonly FakeEmailNotificationService _emailNotificationService = new();
    private readonly OutboxProcessor _processor;

    public OutboxProcessorTests()
    {
        var options = new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new TestDbContext(options, new FakeCurrentUserService(), _clock);
        _processor = new OutboxProcessor(_dbContext, _emailNotificationService, _clock);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task A_pending_password_reset_message_is_sent_and_marked_Processed()
    {
        AddMessage(OutboxMessageType.PasswordResetEmail, new PasswordResetEmailOutboxPayload("customer@example.com", "https://example.com/reset"));
        await _dbContext.SaveChangesAsync();

        await _processor.ProcessPendingAsync();

        _emailNotificationService.PasswordResetEmailsSent.Should().ContainSingle()
            .Which.Should().Be(("customer@example.com", "https://example.com/reset"));

        var message = await _dbContext.OutboxMessages.SingleAsync();
        message.Status.Should().Be(OutboxMessageStatus.Processed);
        message.ProcessedAtUtc.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task A_pending_order_confirmation_message_is_sent_and_marked_Processed()
    {
        var model = new OrderConfirmationEmailModel("ORD-000001", "Jane Doe", _clock.UtcNow, [], 100m, 0m, 10m, 5m, 115m);
        AddMessage(OutboxMessageType.OrderConfirmationEmail, new OrderConfirmationEmailOutboxPayload("customer@example.com", model));
        await _dbContext.SaveChangesAsync();

        await _processor.ProcessPendingAsync();

        _emailNotificationService.OrderConfirmationEmailsSent.Should().ContainSingle();
        var message = await _dbContext.OutboxMessages.SingleAsync();
        message.Status.Should().Be(OutboxMessageStatus.Processed);
    }

    [Fact]
    public async Task A_message_that_fails_to_send_increments_Attempts_and_records_the_error_but_stays_Pending()
    {
        AddMessage(OutboxMessageType.PasswordResetEmail, new PasswordResetEmailOutboxPayload("customer@example.com", "https://example.com/reset"));
        await _dbContext.SaveChangesAsync();
        _emailNotificationService.ThrowOnSend = new InvalidOperationException("disk full");

        await _processor.ProcessPendingAsync();

        var message = await _dbContext.OutboxMessages.SingleAsync();
        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.Attempts.Should().Be(1);
        message.LastError.Should().Be("disk full");
    }

    [Fact]
    public async Task A_message_that_keeps_failing_is_marked_Failed_once_it_reaches_the_attempt_cap()
    {
        AddMessage(OutboxMessageType.PasswordResetEmail, new PasswordResetEmailOutboxPayload("customer@example.com", "https://example.com/reset"));
        await _dbContext.SaveChangesAsync();
        _emailNotificationService.ThrowOnSend = new InvalidOperationException("disk full");

        for (var i = 0; i < 5; i++)
        {
            await _processor.ProcessPendingAsync();
        }

        var message = await _dbContext.OutboxMessages.SingleAsync();
        message.Status.Should().Be(OutboxMessageStatus.Failed);
        message.Attempts.Should().Be(5);
    }

    [Fact]
    public async Task A_failure_on_one_message_does_not_prevent_another_pending_message_from_being_processed()
    {
        var goodMessage = new OutboxMessage
        {
            Type = OutboxMessageType.PasswordResetEmail,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new PasswordResetEmailOutboxPayload("good@example.com", "https://example.com/reset")),
            CreatedAtUtc = _clock.UtcNow,
        };
        _dbContext.OutboxMessages.Add(goodMessage);
        // Corrupt the first message's payload directly, after serialization, to force a deserialize failure independent of the payload record's own shape.
        var badMessage = new OutboxMessage
        {
            Type = OutboxMessageType.PasswordResetEmail,
            PayloadJson = "{ not valid json",
            CreatedAtUtc = _clock.UtcNow,
        };
        _dbContext.OutboxMessages.Add(badMessage);
        await _dbContext.SaveChangesAsync();

        await _processor.ProcessPendingAsync();

        _emailNotificationService.PasswordResetEmailsSent.Should().ContainSingle().Which.ToEmail.Should().Be("good@example.com");
        (await _dbContext.OutboxMessages.FindAsync(goodMessage.Id))!.Status.Should().Be(OutboxMessageStatus.Processed);
        (await _dbContext.OutboxMessages.FindAsync(badMessage.Id))!.Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task Already_Processed_and_Failed_messages_are_left_untouched()
    {
        var processed = new OutboxMessage
        {
            Type = OutboxMessageType.PasswordResetEmail,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new PasswordResetEmailOutboxPayload("a@example.com", "link")),
            Status = OutboxMessageStatus.Processed,
            ProcessedAtUtc = _clock.UtcNow,
            CreatedAtUtc = _clock.UtcNow,
        };
        var failed = new OutboxMessage
        {
            Type = OutboxMessageType.PasswordResetEmail,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new PasswordResetEmailOutboxPayload("b@example.com", "link")),
            Status = OutboxMessageStatus.Failed,
            Attempts = 5,
            CreatedAtUtc = _clock.UtcNow,
        };
        _dbContext.OutboxMessages.AddRange(processed, failed);
        await _dbContext.SaveChangesAsync();

        await _processor.ProcessPendingAsync();

        _emailNotificationService.PasswordResetEmailsSent.Should().BeEmpty();
    }

    private void AddMessage<TPayload>(OutboxMessageType type, TPayload payload) =>
        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Type = type,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload),
            CreatedAtUtc = _clock.UtcNow,
        });
}
