using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Orders;

public interface IOrderService
{
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
}
