namespace ECommerceApp.Application.Marketing.Models;

public record PromotionDto(
    int Id,
    string Name,
    string? Description,
    string? CouponCode,
    string DiscountType,
    decimal DiscountValue,
    string ScopeType,
    int? ScopeCategoryId,
    string? ScopeCategoryName,
    int? ScopeBrandId,
    string? ScopeBrandName,
    int? ScopeProductId,
    string? ScopeProductName,
    decimal? MinimumOrderAmount,
    decimal? MaxDiscountAmount,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    int? MaxTotalUses,
    int? MaxUsesPerCustomer,
    bool IsActive,
    bool IsDeleted);

public record CreatePromotionRequest(
    string Name,
    string? Description,
    string? CouponCode,
    string DiscountType,
    decimal DiscountValue,
    string ScopeType,
    int? ScopeCategoryId,
    int? ScopeBrandId,
    int? ScopeProductId,
    decimal? MinimumOrderAmount,
    decimal? MaxDiscountAmount,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    int? MaxTotalUses,
    int? MaxUsesPerCustomer,
    bool IsActive);

public record UpdatePromotionRequest(
    int Id,
    string Name,
    string? Description,
    string? CouponCode,
    string DiscountType,
    decimal DiscountValue,
    string ScopeType,
    int? ScopeCategoryId,
    int? ScopeBrandId,
    int? ScopeProductId,
    decimal? MinimumOrderAmount,
    decimal? MaxDiscountAmount,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    int? MaxTotalUses,
    int? MaxUsesPerCustomer,
    bool IsActive);

/// <summary>
/// Lean per-line input for promotion evaluation - decoupled from Cart's domain
/// model, mirroring how IPricingService takes raw scalars instead of entities.
/// </summary>
public record PromotionCartLine(int ProductId, int CategoryId, int? BrandId, decimal LineTotal);

/// <summary>
/// LineDiscounts (Milestone 7.4) is one entry per input line, same order,
/// summing exactly to DiscountAmount - a line outside the promotion's scope
/// gets 0. Lets the Checkout Calculation Service compute each line's
/// post-discount, taxable/shippable amount without duplicating scope-
/// matching logic that already lives in PromotionService.Evaluate.
/// </summary>
public record PromotionApplicationDto(
    int PromotionId,
    string Name,
    string CouponCode,
    decimal DiscountAmount,
    IReadOnlyList<decimal> LineDiscounts);
