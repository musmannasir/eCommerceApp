using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Marketing;
using ECommerceApp.Domain.Payments;
using ECommerceApp.Domain.Shipping;

namespace ECommerceApp.Domain.Orders;

/// <summary>
/// A placed order (Milestone 9.1) - created once Checkout's server-side
/// revalidation (Milestone 8.3) succeeds. Everything a customer saw on the
/// Review page is frozen onto this row rather than referenced live: the
/// shipping address is fully copied (Address has no soft delete - Milestone
/// 8.1 - so a customer deleting it later must not corrupt past orders), and
/// the applied shipping method/promotion are both snapshotted by name/amount
/// even though their ids are also kept (both are soft-delete-only, so the FK
/// stays valid, mirroring Cart.AppliedPromotionId's Restrict-delete choice).
/// <see cref="IdempotencyKey"/> is the durable replacement for Milestone
/// 8.3's IMemoryCache-based idempotency token - a unique index on this
/// column means a duplicate PlaceOrder submission (double-click, retry)
/// resolves to the same order even across app restarts, and a genuine race
/// between two identical submissions is caught by the database's unique
/// constraint rather than a check-then-act gap in application code.
/// Stock is not reserved or deducted when an Order is created - that's
/// Milestone 9.3's job ("Stock reservation transaction"); the existing
/// stock-sufficiency check (Milestone 8.3) is a best-effort guard only.
/// Milestone 9.2 charges a (simulated) payment as part of the same
/// CreateOrderAsync call that creates this row - <see cref="Payment"/> is
/// the resulting ledger entry, and <see cref="Status"/> becomes
/// <see cref="OrderStatus.Paid"/> or <see cref="OrderStatus.PaymentFailed"/>
/// based on its outcome. A declined card does not retry in place - the
/// order it produced stays exactly as placed, and trying again means
/// checking out again (a new order, a new idempotency key), not resubmitting
/// this one.
/// Milestone 9.3 reserves stock for every line - via the pre-existing,
/// previously-unwired IInventoryService.ReserveStockAsync (Milestone 3.1) -
/// before the payment charge even runs, finally closing the race this
/// class's own remarks used to describe as open. If any line can't be
/// reserved (or a genuine concurrent race is lost), nothing is charged and
/// <see cref="Status"/> becomes <see cref="OrderStatus.StockReservationFailed"/>
/// with <see cref="StockIssueMessage"/> set; a <see cref="OrderStatus.PaymentFailed"/>
/// order's reservations are released too - only a genuinely
/// <see cref="OrderStatus.Paid"/> order keeps them Active, since holding
/// real inventory for an order nobody actually paid for would be wrong.
/// Milestone 10.2 adds an admin order detail page and the one operation
/// available before a real fulfillment state machine exists (Milestone
/// 10.3): cancelling a <see cref="OrderStatus.Paid"/> order, which releases
/// its reservations and moves it to <see cref="OrderStatus.Cancelled"/>
/// without processing a refund - a cancelled order was never delivered, so
/// there is nothing to return; a refund only follows an approved, received
/// return (Milestone 13.3).
/// Milestone 10.3 adds <see cref="Shipment"/> - shipping a
/// <see cref="OrderStatus.Paid"/> order consumes its stock reservation for
/// good (rather than merely releasing it) and moves it to
/// <see cref="OrderStatus.Shipped"/>, after which it can no longer be
/// cancelled. See <see cref="OrderStatusTransitions"/> for the single
/// definition of which status changes are legal.
/// </summary>
public class Order : AuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string IdempotencyKey { get; set; } = string.Empty;

    public string? ShippingLabel { get; set; }
    public string ShippingFullName { get; set; } = string.Empty;
    public string ShippingPhone { get; set; } = string.Empty;
    public string ShippingLine1 { get; set; } = string.Empty;
    public string? ShippingLine2 { get; set; }
    public string ShippingCity { get; set; } = string.Empty;
    public string? ShippingRegionCode { get; set; }
    public string ShippingPostalCode { get; set; } = string.Empty;
    public string ShippingCountryCode { get; set; } = string.Empty;

    public int? ShippingMethodId { get; set; }
    public string ShippingMethodName { get; set; } = string.Empty;
    public decimal ShippingCost { get; set; }

    public int? PromotionId { get; set; }
    public string? AppliedCouponCode { get; set; }
    public string? AppliedPromotionName { get; set; }
    public decimal PromotionDiscountAmount { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }

    public string? StockIssueMessage { get; set; }

    /// <summary>Staff-only annotation (Milestone 10.2) - never shown to the customer.</summary>
    public string? AdminNotes { get; set; }

    public ShippingMethod? ShippingMethod { get; set; }
    public Promotion? Promotion { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }
    public Shipment? Shipment { get; set; }
}
