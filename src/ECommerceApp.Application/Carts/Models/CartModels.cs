using ECommerceApp.Application.Storefront.Models;

namespace ECommerceApp.Application.Carts.Models;

/// <summary>
/// Identifies whose cart is being operated on - exactly one of UserId/GuestToken
/// is set. The Web layer resolves this per request (authenticated claim, or a
/// guest cookie token) and passes it in; CartService itself has no HttpContext
/// dependency, so it stays Infrastructure-hosted like every other Storefront
/// service instead of needing the Web-hosted exception RecentlyViewedService
/// required.
/// </summary>
public sealed record CartOwner
{
    public string? UserId { get; }
    public string? GuestToken { get; }

    private CartOwner(string? userId, string? guestToken)
    {
        UserId = userId;
        GuestToken = guestToken;
    }

    public static CartOwner ForUser(string userId) => new(userId, null);
    public static CartOwner ForGuest(string guestToken) => new(null, guestToken);
}

public record AddCartItemRequest(int ProductId, int? ProductVariantId, int Quantity);

public record UpdateCartItemQuantityRequest(int CartItemId, int Quantity);

/// <summary>
/// IsAvailable is false when the product (or the selected variant) has since
/// been unpublished, deactivated, or soft-deleted - the line stays visible
/// (customers expect to see what's in their cart, not have it vanish silently)
/// but is excluded from Subtotal/TotalItemCount and can only be removed, not
/// updated. PriceChanged/QuantityExceedsStock are Milestone 6.2's pricing/
/// stock integrity signals, both purely informational - LineTotal always
/// reflects UnitPrice (the live price), never PreviousUnitPrice, and Quantity
/// is never silently changed by a read; QuantityExceedsStock just tells the
/// customer the line's Quantity is now more than AvailableQuantity, so they
/// can choose to adjust it themselves.
/// </summary>
public record CartItemDto(
    int Id,
    int ProductId,
    int? ProductVariantId,
    string ProductName,
    string ProductSlug,
    string? ImagePath,
    string Sku,
    string? VariantDescription,
    decimal UnitPrice,
    decimal? CompareAtPrice,
    int? DiscountPercent,
    int Quantity,
    decimal LineTotal,
    ProductStockState StockState,
    int AvailableQuantity,
    bool IsAvailable,
    bool PriceChanged,
    decimal? PreviousUnitPrice,
    bool QuantityExceedsStock);

/// <summary>
/// AppliedCouponCode/AppliedPromotionName/PromotionDiscount are null/zero when
/// no promotion is applied. Subtotal keeps its pre-discount meaning; Total is
/// Subtotal minus PromotionDiscount (Milestone 7.1). EstimatedTax (Milestone
/// 7.2) is computed against the store's configured default tax jurisdiction,
/// not a real customer destination (no Address entity exists until Milestone
/// 8.1) - and against pre-discount line totals, since allocating a
/// cart-level discount across lines for tax purposes is the Checkout
/// Calculation Service's job (Milestone 7.4), not this estimate's.
/// EstimatedTaxRateConfigured is false when no tax rate at all has been set
/// up for the store's default jurisdiction, distinct from a genuine 0% rate.
/// EstimatedShipping (Milestone 7.3) is the cheapest available shipping
/// method's cost against the store's default shipping jurisdiction and the
/// cart's total weight, same estimate-only reasoning as EstimatedTax;
/// EstimatedShippingRateConfigured is false when no active method exists
/// for that jurisdiction at all, distinct from a genuine free/zero-cost one.
/// </summary>
public record CartDto(
    int? Id,
    IReadOnlyList<CartItemDto> Items,
    int TotalItemCount,
    decimal Subtotal,
    string? AppliedCouponCode,
    string? AppliedPromotionName,
    decimal PromotionDiscount,
    decimal Total,
    decimal EstimatedTax,
    bool EstimatedTaxRateConfigured,
    decimal EstimatedShipping,
    bool EstimatedShippingRateConfigured);
