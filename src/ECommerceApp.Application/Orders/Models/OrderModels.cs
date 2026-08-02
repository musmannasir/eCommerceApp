using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Payments.Models;
using ECommerceApp.Application.Returns.Models;
using ECommerceApp.Application.Shipping.Models;

namespace ECommerceApp.Application.Orders.Models;

public record OrderDto(
    int Id,
    string OrderNumber,
    string Status,
    DateTime PlacedAtUtc,
    string? ShippingLabel,
    string ShippingFullName,
    string ShippingPhone,
    string ShippingLine1,
    string? ShippingLine2,
    string ShippingCity,
    string? ShippingRegionCode,
    string ShippingPostalCode,
    string ShippingCountryCode,
    string ShippingMethodName,
    decimal ShippingCost,
    string? AppliedCouponCode,
    string? AppliedPromotionName,
    decimal PromotionDiscountAmount,
    decimal Subtotal,
    decimal Tax,
    decimal GrandTotal,
    string PaymentStatus,
    string? MaskedCardNumber,
    string? CardBrand,
    string? DeclineReason,
    string? StockIssueMessage,
    string? AdminNotes,
    string? Carrier,
    string? TrackingNumber,
    DateTime? ShippedAtUtc,
    DateTime? DeliveredAtUtc,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<ReturnRequestDto> ReturnRequests);

public record OrderItemDto(
    int Id,
    int ProductId,
    int? ProductVariantId,
    string ProductName,
    string Sku,
    string? VariantDescription,
    string? ImagePath,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

/// <summary>The admin order queue's row shape - deliberately lighter than OrderDto, the same relationship PurchaseOrderListItemDto has to the full purchase order.</summary>
public record OrderListItemDto(
    int Id,
    string OrderNumber,
    string CustomerName,
    string Status,
    int ItemCount,
    decimal GrandTotal,
    DateTime PlacedAtUtc);

public record ShipOrderRequest(string Carrier, string TrackingNumber);

/// <summary>
/// The customer-facing "My Orders" dashboard (Milestone 11.1) - TotalSpent
/// only counts orders whose payment actually succeeded (Payment.Status ==
/// Succeeded), which covers Paid/Shipped/Delivered/Cancelled (cancelling
/// does not reverse the charge - Milestone 10.2) and excludes
/// PaymentFailed/StockReservationFailed, which were never actually charged.
/// </summary>
public record CustomerOrderDashboardDto(int TotalOrders, decimal TotalSpent, PagedResult<OrderListItemDto> Orders);

public record OrderQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? Status { get; init; }
}

/// <summary>
/// Everything needed to persist an order is already resolved and validated
/// by the time Checkout's PlaceOrder action calls this - creating an Order
/// is a pure "freeze this already-checked data" operation, not a second
/// round of validation. The one exception is <see cref="Payment"/> - the raw
/// card input is charged for real (against the simulated gateway) as part of
/// this same call, since Milestone 9.2 treats "place the order" and "charge
/// the card" as one atomic step. Milestone 9.3 adds a further step before
/// the charge - reserving stock for every line - so nothing is ever charged
/// for an order whose stock couldn't actually be secured.
/// </summary>
public record CreateOrderRequest(
    string UserId,
    string IdempotencyKey,
    AddressDto Address,
    int? AppliedPromotionId,
    ShippingOptionDto ShippingOption,
    IReadOnlyList<CartItemDto> Items,
    CheckoutCalculationResult Calculation,
    ChargeRequest Payment);
