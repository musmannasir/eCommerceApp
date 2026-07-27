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

public record PromotionApplicationDto(
    int PromotionId,
    string Name,
    string CouponCode,
    decimal DiscountAmount);
