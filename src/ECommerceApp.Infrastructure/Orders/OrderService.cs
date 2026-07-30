using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Orders;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Orders;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Orders;

/// <summary>
/// Milestone 9.1 - persists an Order/its OrderItems from data Checkout has
/// already fully validated (CheckoutController.PlaceOrder). Does not touch
/// InventoryItem.QuantityReserved/QuantityOnHand - stock reservation is
/// Milestone 9.3's job; this service only freezes what was agreed at Review.
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly ApplicationDbContext _dbContext;

    public OrderService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<OrderDto>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await FindByIdempotencyKeyAsync(request.UserId, request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return Result.Success(ToDto(existing));
        }

        var address = request.Address;
        var calculation = request.Calculation;

        var order = new Order
        {
            UserId = request.UserId,
            IdempotencyKey = request.IdempotencyKey,
            OrderNumber = string.Empty,
            Status = OrderStatus.Pending,
            ShippingLabel = address.Label,
            ShippingFullName = address.FullName,
            ShippingPhone = address.Phone,
            ShippingLine1 = address.Line1,
            ShippingLine2 = address.Line2,
            ShippingCity = address.City,
            ShippingRegionCode = address.RegionCode,
            ShippingPostalCode = address.PostalCode,
            ShippingCountryCode = address.CountryCode,
            ShippingMethodId = request.ShippingOption.ShippingMethodId,
            ShippingMethodName = request.ShippingOption.Name,
            ShippingCost = request.ShippingOption.Cost,
            PromotionId = request.AppliedPromotionId,
            AppliedCouponCode = calculation.AppliedCouponCode,
            AppliedPromotionName = calculation.AppliedPromotionName,
            PromotionDiscountAmount = calculation.PromotionDiscount,
            Subtotal = calculation.Subtotal,
            Tax = calculation.Tax,
            GrandTotal = calculation.GrandTotal,
            Items = request.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductVariantId = i.ProductVariantId,
                ProductName = i.ProductName,
                Sku = i.Sku,
                VariantDescription = i.VariantDescription,
                ImagePath = i.ImagePath,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
            }).ToList(),
        };

        _dbContext.Orders.Add(order);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent identical submission may have won the race on the
            // unique IdempotencyKey index between our initial check above and
            // this insert - if so, replay that order instead of failing.
            var raced = await FindByIdempotencyKeyAsync(request.UserId, request.IdempotencyKey, cancellationToken);
            if (raced is not null)
            {
                return Result.Success(ToDto(raced));
            }

            throw;
        }

        order.OrderNumber = $"ORD-{order.Id:D6}";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(order));
    }

    public async Task<Result<OrderDto>> GetByIdempotencyKeyAsync(string userId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var order = await FindByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
        return order is null
            ? Result.Failure<OrderDto>(Error.NotFound("order.not_found", "Order not found."))
            : Result.Success(ToDto(order));
    }

    public async Task<Result<OrderDto>> GetByOrderNumberAsync(string userId, string orderNumber, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.UserId == userId, cancellationToken);

        return order is null
            ? Result.Failure<OrderDto>(Error.NotFound("order.not_found", "Order not found."))
            : Result.Success(ToDto(order));
    }

    private Task<Order?> FindByIdempotencyKeyAsync(string userId, string idempotencyKey, CancellationToken cancellationToken) =>
        _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey && o.UserId == userId, cancellationToken);

    private static OrderDto ToDto(Order order) => new(
        order.Id, order.OrderNumber, order.Status.ToString(), order.CreatedAtUtc,
        order.ShippingLabel, order.ShippingFullName, order.ShippingPhone, order.ShippingLine1, order.ShippingLine2,
        order.ShippingCity, order.ShippingRegionCode, order.ShippingPostalCode, order.ShippingCountryCode,
        order.ShippingMethodName, order.ShippingCost,
        order.AppliedCouponCode, order.AppliedPromotionName, order.PromotionDiscountAmount,
        order.Subtotal, order.Tax, order.GrandTotal,
        order.Items.Select(ToItemDto).ToList());

    private static OrderItemDto ToItemDto(OrderItem item) => new(
        item.Id, item.ProductId, item.ProductVariantId, item.ProductName, item.Sku, item.VariantDescription,
        item.ImagePath, item.UnitPrice, item.Quantity, item.UnitPrice * item.Quantity);
}
