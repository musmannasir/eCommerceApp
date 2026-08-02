using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Orders;

public interface IOrderService
{
    /// <summary>Admin order queue (Milestone 10.1) - every order, not scoped to one customer.</summary>
    Task<Result<PagedResult<OrderListItemDto>>> GetPagedAsync(OrderQuery query, CancellationToken cancellationToken = default);

    /// <summary>Admin order detail (Milestone 10.2) - not ownership-scoped, unlike GetByOrderNumberAsync.</summary>
    Task<Result<OrderDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a Paid order and releases its active stock reservations.
    /// Only a Paid order can be cancelled - a PaymentFailed/StockReservationFailed
    /// order never held a reservation or a charge, so there is nothing to
    /// reverse. Does not process a refund (Milestone 13.3's job).
    /// </summary>
    Task<Result<OrderDto>> CancelAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<OrderDto>> UpdateAdminNotesAsync(int id, string? notes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ships a Paid order (Milestone 10.3): consumes its active stock
    /// reservations for good (rather than releasing them), records a
    /// Shipment, and moves it to OrderStatus.Shipped. Only a Paid order can
    /// be shipped - see OrderStatusTransitions for the full legal graph.
    /// </summary>
    Task<Result<OrderDto>> ShipAsync(int id, ShipOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Marks a Shipped order Delivered - the terminal, successful outcome.</summary>
    Task<Result<OrderDto>> MarkDeliveredAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an order, or - if a previous call already succeeded with the
    /// same <see cref="CreateOrderRequest.IdempotencyKey"/> - returns that
    /// existing order instead of creating a duplicate. Safe under a genuine
    /// race between two identical submissions: the unique index on
    /// IdempotencyKey catches it at the database level even if both requests
    /// pass an initial "does it exist" check at the same time.
    /// </summary>
    Task<Result<OrderDto>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ownership-scoped like IAddressService - another customer's order id/number returns NotFound, never their data.</summary>
    Task<Result<OrderDto>> GetByIdempotencyKeyAsync(string userId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<Result<OrderDto>> GetByOrderNumberAsync(string userId, string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>Customer-facing order dashboard (Milestone 11.1) - ownership-scoped, unlike GetPagedAsync's admin-wide queue.</summary>
    Task<Result<CustomerOrderDashboardDto>> GetDashboardAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default);
}
