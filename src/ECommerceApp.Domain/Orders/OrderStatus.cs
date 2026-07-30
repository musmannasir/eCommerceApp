namespace ECommerceApp.Domain.Orders;

/// <summary>
/// Milestone 9.1 introduced this enum with just <see cref="Pending"/>.
/// Milestone 9.2 added <see cref="Paid"/>/<see cref="PaymentFailed"/> for a
/// (simulated) payment charge's two outcomes. Milestone 9.3 adds
/// <see cref="StockReservationFailed"/> - stock is now reserved for every
/// line *before* the payment charge runs, so an order that can't secure its
/// stock is never charged at all; this is a genuinely different outcome
/// from a declined card (the remedy is picking different items/quantities,
/// not a different payment method), which is why it's its own value rather
/// than reusing <see cref="PaymentFailed"/>. The fulfillment state machine
/// (Milestone 10.3) adds its own states when it exists; adding them now
/// would be speculative.
/// </summary>
public enum OrderStatus
{
    Pending,
    Paid,
    PaymentFailed,
    StockReservationFailed,
}
