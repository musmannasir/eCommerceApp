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
/// than reusing <see cref="PaymentFailed"/>. Milestone 10.2 adds
/// <see cref="Cancelled"/> - the one order-lifecycle operation available
/// before Milestone 10.3 builds a real fulfillment/shipment state machine;
/// cancelling releases the order's stock reservations but does not process
/// a refund (Milestone 13.3's job, a separate transaction). Milestone 10.3
/// adds <see cref="Shipped"/>/<see cref="Delivered"/> - shipping consumes
/// the order's stock reservation for good (Milestone 3.1's
/// <see cref="Inventory.ReservationStatus.Consumed"/>/<see cref="Inventory.StockMovementType.SaleCompletion"/>,
/// pre-provisioned but unused until now) and records a <see cref="Shipment"/>;
/// once shipped, an order can no longer be cancelled - there is no
/// return/refund flow yet, so a mis-shipped order stays exactly as it is.
/// See <see cref="OrderStatusTransitions"/> for the single, centralized
/// definition of which of these transitions is legal from which state -
/// every status change in <c>OrderService</c> goes through it rather than
/// each operation checking its own ad-hoc condition.
/// </summary>
public enum OrderStatus
{
    Pending,
    Paid,
    PaymentFailed,
    StockReservationFailed,
    Cancelled,
    Shipped,
    Delivered,
}
