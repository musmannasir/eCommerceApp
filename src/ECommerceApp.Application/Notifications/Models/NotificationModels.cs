namespace ECommerceApp.Application.Notifications.Models;

public record PasswordResetEmailModel(string ResetLink);

public record OrderConfirmationEmailModel(
    string OrderNumber,
    string CustomerName,
    DateTime PlacedAtUtc,
    IReadOnlyList<OrderConfirmationEmailItemModel> Items,
    decimal Subtotal,
    decimal PromotionDiscountAmount,
    decimal Tax,
    decimal ShippingCost,
    decimal GrandTotal);

public record OrderConfirmationEmailItemModel(string ProductName, string? VariantDescription, int Quantity, decimal LineTotal);

/// <summary>Milestone 15.2 - the JSON shape stored in <see cref="ECommerceApp.Domain.Notifications.OutboxMessage.PayloadJson"/> for a queued password-reset email.</summary>
public record PasswordResetEmailOutboxPayload(string ToEmail, string ResetLink);

/// <summary>Milestone 15.2 - the JSON shape stored in <see cref="ECommerceApp.Domain.Notifications.OutboxMessage.PayloadJson"/> for a queued order-confirmation email.</summary>
public record OrderConfirmationEmailOutboxPayload(string ToEmail, OrderConfirmationEmailModel Model);
