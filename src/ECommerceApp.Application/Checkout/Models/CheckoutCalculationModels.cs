namespace ECommerceApp.Application.Checkout.Models;

/// <summary>
/// Lean per-line input for checkout calculation - decoupled from Cart's
/// domain model, mirroring how IPricingService/IPromotionService/
/// ITaxService/IShippingService all take raw scalars instead of entities.
/// TotalWeight is the line's contribution to the order's shippable weight
/// (Product.Weight * Quantity, already computed by the caller - 0 for a
/// product with no recorded weight).
/// </summary>
public record CheckoutLineDto(
    int ProductId,
    int CategoryId,
    int? BrandId,
    string TaxCategory,
    bool IsTaxable,
    decimal TotalWeight,
    decimal LineTotal);

/// <summary>
/// Combines Promotion + Tax + Shipping into one final total (Milestone
/// 7.4). Subtotal is pre-discount; DiscountedSubtotal is Subtotal minus
/// PromotionDiscount - Tax and Shipping are both computed against
/// DiscountedSubtotal (and each taxable line's post-discount amount), the
/// gap Milestones 7.2/7.3 each explicitly deferred to this service.
/// GrandTotal is DiscountedSubtotal + Tax + Shipping.
/// </summary>
public record CheckoutCalculationResult(
    decimal Subtotal,
    decimal PromotionDiscount,
    string? AppliedCouponCode,
    string? AppliedPromotionName,
    decimal DiscountedSubtotal,
    decimal Tax,
    bool TaxRateConfigured,
    decimal Shipping,
    bool ShippingRateConfigured,
    decimal GrandTotal);

/// <summary>
/// The cart's currently-available lines, in the shape
/// ICheckoutCalculationService.CalculateAsync needs, plus the resolved
/// (valid-or-null) applied promotion id (Milestone 8.2 - what the Checkout
/// flow uses to compute real destination-based totals against a selected
/// Address, instead of the store-default-jurisdiction estimate the Cart page
/// shows).
/// </summary>
public record CheckoutInputDto(IReadOnlyList<CheckoutLineDto> Lines, int? AppliedPromotionId);
