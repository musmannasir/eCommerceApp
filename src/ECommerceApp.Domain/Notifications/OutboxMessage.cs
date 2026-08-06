using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Notifications;

/// <summary>
/// Milestone 15.2 - a durable "intent to send" row, written by the same
/// <c>SaveChangesAsync</c> call that persists the business change it's
/// about (a paid <see cref="Orders.Order"/>, a password-reset request), so
/// the two either both commit or neither does. Uses the mutable
/// <see cref="AuditableEntity"/> base like <see cref="Inventory.InventoryReservation"/>,
/// since - unlike a pure ledger row such as <see cref="Payments.Payment"/> -
/// this one has a real Pending -> Processed/Failed lifecycle and gets
/// updated in place as delivery is attempted.
/// </summary>
public class OutboxMessage : AuditableEntity
{
    public OutboxMessageType Type { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
