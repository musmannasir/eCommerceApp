namespace ECommerceApp.Domain.Orders;

/// <summary>
/// The single, centralized definition of which <see cref="OrderStatus"/>
/// transitions are legal (Milestone 10.3) - a pure lookup, no I/O, so it can
/// be called from anywhere that changes an order's status without adding a
/// dependency. Before this, each operation (order creation, cancellation)
/// checked its own ad-hoc condition against <see cref="OrderStatus"/>;
/// every status change now goes through <see cref="CanTransition"/> instead,
/// so the legal state graph exists in exactly one place.
/// </summary>
public static class OrderStatusTransitions
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Paid, OrderStatus.PaymentFailed, OrderStatus.StockReservationFailed },
        [OrderStatus.Paid] = new[] { OrderStatus.Cancelled, OrderStatus.Shipped },
        [OrderStatus.Shipped] = new[] { OrderStatus.Delivered },
        [OrderStatus.PaymentFailed] = Array.Empty<OrderStatus>(),
        [OrderStatus.StockReservationFailed] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
        [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}
