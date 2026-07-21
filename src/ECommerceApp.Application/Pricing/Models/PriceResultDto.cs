namespace ECommerceApp.Application.Pricing.Models;

/// <summary>
/// Single source of truth for "what does this product/variant cost" (Milestone 5.2).
/// PromotionAdjustment is always 0 for now - Promotions don't exist until Milestone 7.1 -
/// so FinalPrice currently always equals VariantPrice; the field exists so callers never
/// need to change once promotions land. IsTaxInclusive is a static store-wide config flag
/// (Store:PricesIncludeTax) rather than a computed amount - the real tax-rate engine is
/// Milestone 7.2's scope.
/// </summary>
public record PriceResultDto(
    decimal BasePrice,
    decimal VariantPrice,
    decimal PromotionAdjustment,
    decimal FinalPrice,
    decimal? CompareAtPrice,
    decimal? DiscountAmount,
    int? DiscountPercent,
    bool IsTaxInclusive);
