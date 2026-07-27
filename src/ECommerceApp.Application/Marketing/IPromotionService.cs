using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Marketing;

public interface IPromotionService
{
    Task<Result<PromotionDto>> CreateAsync(CreatePromotionRequest request, CancellationToken cancellationToken = default);
    Task<Result<PromotionDto>> UpdateAsync(UpdatePromotionRequest request, CancellationToken cancellationToken = default);
    Task<Result<PromotionDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<PromotionDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up an active, code-based promotion by its coupon code (case-insensitive)
    /// and validates it against the given cart snapshot: date window, IsActive,
    /// MinimumOrderAmount (checked against subtotal), and scope (a
    /// Category/Brand/Product-scoped promotion only discounts the matching
    /// lines - if none match, the promotion doesn't apply). Returns the
    /// computed discount amount, already capped by MaxDiscountAmount (for a
    /// percentage discount) and by the eligible amount itself (a discount can
    /// never exceed what it's discounting). MaxTotalUses/MaxUsesPerCustomer are
    /// not enforced here - see Promotion's doc comment.
    /// </summary>
    Task<Result<PromotionApplicationDto>> FindApplicablePromotionAsync(
        string couponCode, IReadOnlyList<PromotionCartLine> lines, decimal subtotal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-runs the same validation as FindApplicablePromotionAsync, but against
    /// a promotion already applied to a cart (looked up by id, not by code) -
    /// used by CartService on every cart read to silently clear a promotion
    /// that's become invalid since it was applied (cart contents changed, the
    /// promotion expired, an admin deactivated it, etc.).
    /// </summary>
    Task<Result<PromotionApplicationDto>> ValidateAppliedPromotionAsync(
        int promotionId, IReadOnlyList<PromotionCartLine> lines, decimal subtotal, CancellationToken cancellationToken = default);
}
