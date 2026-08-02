using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Inventory;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Application.Orders;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Application.Payments;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Inventory;
using ECommerceApp.Domain.Orders;
using ECommerceApp.Domain.Payments;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Orders;

/// <summary>
/// Milestone 9.1 persists an Order/its OrderItems from data Checkout has
/// already fully validated (CheckoutController.PlaceOrder). Milestone 9.2
/// charges the card (via the simulated IPaymentGateway) as part of this same
/// call - placing an order and charging its payment method are treated as
/// one atomic step, not two separate ones a caller could invoke out of order
/// or only half-complete. Milestone 9.3 adds a further step before the
/// charge - reserving stock for every line via the pre-existing, previously
/// unwired IInventoryService.ReserveStockAsync (Milestone 3.1) - so nothing
/// is ever charged for an order whose stock couldn't actually be secured,
/// finally closing the race this class's own history has flagged since
/// Milestone 8.3.
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IInventoryService _inventoryService;
    private readonly IClock _clock;

    public OrderService(ApplicationDbContext dbContext, IPaymentGateway paymentGateway, IInventoryService inventoryService, IClock clock)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
        _inventoryService = inventoryService;
        _clock = clock;
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
            // this insert - if so, replay that order instead of failing (and,
            // critically, without ever reserving stock or charging the card
            // a second time).
            var raced = await FindByIdempotencyKeyAsync(request.UserId, request.IdempotencyKey, cancellationToken);
            if (raced is not null)
            {
                return Result.Success(ToDto(raced));
            }

            throw;
        }

        order.OrderNumber = $"ORD-{order.Id:D6}";

        var reservationIds = new List<int>();
        string? stockIssueMessage = null;

        foreach (var item in order.Items)
        {
            var inventoryItem = await FindBestInventoryItemAsync(item.ProductId, item.ProductVariantId, cancellationToken);
            if (inventoryItem is null)
            {
                // No InventoryItem row at all for this product/variant means
                // it's untracked - the same leniency untracked inventory
                // already gets on the Cart page and product detail (nothing
                // to reserve against).
                continue;
            }

            Result<InventoryReservationDto> reserveResult;
            try
            {
                reserveResult = await _inventoryService.ReserveStockAsync(
                    new ReserveStockRequest(inventoryItem.Id, item.Quantity, "Order", order.Id.ToString()), cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone else's reservation won the race for this exact
                // inventory row between our read and our write - treated the
                // same as "not enough was available," not left to crash.
                reserveResult = Result.Failure<InventoryReservationDto>(Error.Validation(
                    "inventory.reservation_conflict", "Stock changed while reserving this item - please try again."));
            }

            if (reserveResult.IsFailure)
            {
                stockIssueMessage = $"{item.ProductName}: {reserveResult.FirstError.Message}";
                break;
            }

            reservationIds.Add(reserveResult.Value.Id);
        }

        if (stockIssueMessage is not null)
        {
            // All-or-nothing for this order - release whatever already
            // succeeded rather than leaving some lines holding reserved
            // stock for an order that, as a whole, can never be fulfilled.
            foreach (var reservationId in reservationIds)
            {
                await _inventoryService.ReleaseReservationAsync(reservationId, cancellationToken);
            }

            order.Status = OrderStatus.StockReservationFailed;
            order.StockIssueMessage = stockIssueMessage;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(ToDto(order));
        }

        var chargeResult = await _paymentGateway.ChargeAsync(request.Payment with { Amount = order.GrandTotal }, cancellationToken);
        order.Status = chargeResult.Succeeded ? OrderStatus.Paid : OrderStatus.PaymentFailed;
        order.Payment = new Payment
        {
            Amount = order.GrandTotal,
            Status = chargeResult.Succeeded ? PaymentStatus.Succeeded : PaymentStatus.Failed,
            MaskedCardNumber = chargeResult.MaskedCardNumber,
            CardBrand = chargeResult.CardBrand,
            DeclineReason = chargeResult.DeclineReason,
            ProcessedAtUtc = _clock.UtcNow,
        };

        if (!chargeResult.Succeeded)
        {
            // A declined card shouldn't hold real inventory hostage - only a
            // genuinely paid order keeps its reservations Active.
            foreach (var reservationId in reservationIds)
            {
                await _inventoryService.ReleaseReservationAsync(reservationId, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(order));
    }

    public async Task<Result<PagedResult<OrderListItemDto>>> GetPagedAsync(OrderQuery query, CancellationToken cancellationToken = default)
    {
        var orders = _dbContext.Orders.Include(o => o.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            orders = orders.Where(o => o.OrderNumber.Contains(query.Search) || o.ShippingFullName.Contains(query.Search));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<OrderStatus>(query.Status, out var status))
        {
            orders = orders.Where(o => o.Status == status);
        }

        orders = orders.OrderByDescending(o => o.Id);

        var totalCount = await orders.CountAsync(cancellationToken);
        var page = await orders
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var items = page.Select(o => new OrderListItemDto(
            o.Id, o.OrderNumber, o.ShippingFullName, o.Status.ToString(), o.Items.Count, o.GrandTotal, o.CreatedAtUtc)).ToList();

        return Result.Success(new PagedResult<OrderListItemDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<CustomerOrderDashboardDto>> GetDashboardAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var orders = _dbContext.Orders.Include(o => o.Items).Where(o => o.UserId == userId);

        var totalOrders = await orders.CountAsync(cancellationToken);
        var totalSpent = await orders
            .Where(o => o.Payment != null && o.Payment.Status == PaymentStatus.Succeeded)
            .SumAsync(o => o.GrandTotal, cancellationToken);

        var pageItems = await orders
            .OrderByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var items = pageItems.Select(o => new OrderListItemDto(
            o.Id, o.OrderNumber, o.ShippingFullName, o.Status.ToString(), o.Items.Count, o.GrandTotal, o.CreatedAtUtc)).ToList();

        return Result.Success(new CustomerOrderDashboardDto(
            totalOrders, totalSpent, new PagedResult<OrderListItemDto>(items, totalOrders, page, pageSize)));
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
            .Include(o => o.Payment)
            .Include(o => o.Shipment)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.UserId == userId, cancellationToken);

        return order is null
            ? Result.Failure<OrderDto>(Error.NotFound("order.not_found", "Order not found."))
            : Result.Success(ToDto(order));
    }

    public async Task<Result<OrderDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.Shipment)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order is null
            ? Result.Failure<OrderDto>(Error.NotFound("order.not_found", "Order not found."))
            : Result.Success(ToDto(order));
    }

    public async Task<Result<OrderDto>> CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.Shipment)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(Error.NotFound("order.not_found", "Order not found."));
        }

        if (!OrderStatusTransitions.CanTransition(order.Status, OrderStatus.Cancelled))
        {
            return Result.Failure<OrderDto>(Error.Validation(
                "order.not_cancellable", "Only a paid order can be cancelled."));
        }

        var activeReservations = await _dbContext.InventoryReservations
            .Where(r => r.ReferenceType == "Order" && r.ReferenceId == order.Id.ToString() && r.Status == ReservationStatus.Active)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        foreach (var reservationId in activeReservations)
        {
            await _inventoryService.ReleaseReservationAsync(reservationId, cancellationToken);
        }

        order.Status = OrderStatus.Cancelled;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(order));
    }

    public async Task<Result<OrderDto>> UpdateAdminNotesAsync(int id, string? notes, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.Shipment)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(Error.NotFound("order.not_found", "Order not found."));
        }

        order.AdminNotes = notes;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(order));
    }

    public async Task<Result<OrderDto>> ShipAsync(int id, ShipOrderRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.Shipment)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(Error.NotFound("order.not_found", "Order not found."));
        }

        if (!OrderStatusTransitions.CanTransition(order.Status, OrderStatus.Shipped))
        {
            return Result.Failure<OrderDto>(Error.Validation(
                "order.not_shippable", "Only a paid order can be shipped."));
        }

        var activeReservations = await _dbContext.InventoryReservations
            .Where(r => r.ReferenceType == "Order" && r.ReferenceId == order.Id.ToString() && r.Status == ReservationStatus.Active)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        foreach (var reservationId in activeReservations)
        {
            await _inventoryService.ConsumeReservationAsync(reservationId, cancellationToken);
        }

        var utcNow = _clock.UtcNow;
        order.Shipment = new Shipment
        {
            OrderId = order.Id,
            Carrier = request.Carrier,
            TrackingNumber = request.TrackingNumber,
            ShippedAtUtc = utcNow,
        };
        order.Status = OrderStatus.Shipped;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(order));
    }

    public async Task<Result<OrderDto>> MarkDeliveredAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.Shipment)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(Error.NotFound("order.not_found", "Order not found."));
        }

        if (!OrderStatusTransitions.CanTransition(order.Status, OrderStatus.Delivered))
        {
            return Result.Failure<OrderDto>(Error.Validation(
                "order.not_deliverable", "Only a shipped order can be marked delivered."));
        }

        order.Shipment!.DeliveredAtUtc = _clock.UtcNow;
        order.Status = OrderStatus.Delivered;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(order));
    }

    private Task<Order?> FindByIdempotencyKeyAsync(string userId, string idempotencyKey, CancellationToken cancellationToken) =>
        _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.Shipment)
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey && o.UserId == userId, cancellationToken);

    /// <summary>
    /// No warehouse is ever chosen anywhere in Cart/Checkout today (the Cart
    /// page's own stock check sums across every warehouse), so this picks
    /// whichever warehouse has the most available stock for this line - a
    /// simple, defensible policy given nothing upstream expresses a
    /// preference. A product/variant with no InventoryItem row at all is
    /// untracked (returns null, handled by the caller).
    /// </summary>
    private async Task<InventoryItem?> FindBestInventoryItemAsync(int productId, int? productVariantId, CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.InventoryItems
            .Where(i => i.ProductId == productId && i.ProductVariantId == productVariantId)
            .ToListAsync(cancellationToken);

        return candidates.OrderByDescending(i => i.QuantityAvailable).FirstOrDefault();
    }

    private static OrderDto ToDto(Order order) => new(
        order.Id, order.OrderNumber, order.Status.ToString(), order.CreatedAtUtc,
        order.ShippingLabel, order.ShippingFullName, order.ShippingPhone, order.ShippingLine1, order.ShippingLine2,
        order.ShippingCity, order.ShippingRegionCode, order.ShippingPostalCode, order.ShippingCountryCode,
        order.ShippingMethodName, order.ShippingCost,
        order.AppliedCouponCode, order.AppliedPromotionName, order.PromotionDiscountAmount,
        order.Subtotal, order.Tax, order.GrandTotal,
        order.Payment?.Status.ToString() ?? order.Status.ToString(),
        order.Payment?.MaskedCardNumber, order.Payment?.CardBrand, order.Payment?.DeclineReason,
        order.StockIssueMessage,
        order.AdminNotes,
        order.Shipment?.Carrier, order.Shipment?.TrackingNumber, order.Shipment?.ShippedAtUtc, order.Shipment?.DeliveredAtUtc,
        order.Items.Select(ToItemDto).ToList());

    private static OrderItemDto ToItemDto(OrderItem item) => new(
        item.Id, item.ProductId, item.ProductVariantId, item.ProductName, item.Sku, item.VariantDescription,
        item.ImagePath, item.UnitPrice, item.Quantity, item.UnitPrice * item.Quantity);
}
