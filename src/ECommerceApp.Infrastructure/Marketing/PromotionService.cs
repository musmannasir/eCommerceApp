using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Marketing;
using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Marketing;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Marketing;

/// <summary>
/// Admin CRUD mirrors HomePageBannerService exactly. FindApplicablePromotionAsync
/// is the customer-facing evaluation path used by CartService (Milestone 7.1) -
/// it only ever matches code-based promotions (CouponCode not null), since an
/// automatic promotion has no code for a customer to enter; automatic
/// promotions are creatable here for completeness but aren't auto-applied to
/// carts yet (no design exists yet for resolving precedence among several at
/// once, and the project's "no stacking" v1 rule would need to pick one).
/// </summary>
public sealed class PromotionService : IPromotionService
{
    private readonly ApplicationDbContext _dbContext;

    public PromotionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PromotionDto>> CreateAsync(CreatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        var codeConflict = await CouponCodeInUseAsync(request.CouponCode, null, cancellationToken);
        if (codeConflict)
        {
            return Result.Failure<PromotionDto>(Error.Conflict("promotion.coupon_code_in_use", "This coupon code is already in use by another promotion."));
        }

        var promotion = new Promotion
        {
            Name = request.Name,
            Description = request.Description,
            CouponCode = request.CouponCode,
            DiscountType = Enum.Parse<PromotionDiscountType>(request.DiscountType),
            DiscountValue = request.DiscountValue,
            ScopeType = Enum.Parse<PromotionScopeType>(request.ScopeType),
            ScopeCategoryId = request.ScopeCategoryId,
            ScopeBrandId = request.ScopeBrandId,
            ScopeProductId = request.ScopeProductId,
            MinimumOrderAmount = request.MinimumOrderAmount,
            MaxDiscountAmount = request.MaxDiscountAmount,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            MaxTotalUses = request.MaxTotalUses,
            MaxUsesPerCustomer = request.MaxUsesPerCustomer,
            IsActive = request.IsActive,
        };

        _dbContext.Promotions.Add(promotion);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await ToDtoAsync(promotion, cancellationToken));
    }

    public async Task<Result<PromotionDto>> UpdateAsync(UpdatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        var promotion = await _dbContext.Promotions.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (promotion is null)
        {
            return Result.Failure<PromotionDto>(Error.NotFound("promotion.not_found", "Promotion not found."));
        }

        var codeConflict = await CouponCodeInUseAsync(request.CouponCode, request.Id, cancellationToken);
        if (codeConflict)
        {
            return Result.Failure<PromotionDto>(Error.Conflict("promotion.coupon_code_in_use", "This coupon code is already in use by another promotion."));
        }

        promotion.Name = request.Name;
        promotion.Description = request.Description;
        promotion.CouponCode = request.CouponCode;
        promotion.DiscountType = Enum.Parse<PromotionDiscountType>(request.DiscountType);
        promotion.DiscountValue = request.DiscountValue;
        promotion.ScopeType = Enum.Parse<PromotionScopeType>(request.ScopeType);
        promotion.ScopeCategoryId = request.ScopeCategoryId;
        promotion.ScopeBrandId = request.ScopeBrandId;
        promotion.ScopeProductId = request.ScopeProductId;
        promotion.MinimumOrderAmount = request.MinimumOrderAmount;
        promotion.MaxDiscountAmount = request.MaxDiscountAmount;
        promotion.StartsAtUtc = request.StartsAtUtc;
        promotion.EndsAtUtc = request.EndsAtUtc;
        promotion.MaxTotalUses = request.MaxTotalUses;
        promotion.MaxUsesPerCustomer = request.MaxUsesPerCustomer;
        promotion.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(await ToDtoAsync(promotion, cancellationToken));
    }

    public async Task<Result<PromotionDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var promotion = await _dbContext.Promotions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return promotion is null
            ? Result.Failure<PromotionDto>(Error.NotFound("promotion.not_found", "Promotion not found."))
            : Result.Success(await ToDtoAsync(promotion, cancellationToken));
    }

    public async Task<Result<PagedResult<PromotionDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var promotions = query.OnlyDeleted
            ? _dbContext.Promotions.IgnoreQueryFilters().Where(p => p.IsDeleted)
            : _dbContext.Promotions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            promotions = promotions.Where(p => p.Name.Contains(query.Search) || (p.CouponCode != null && p.CouponCode.Contains(query.Search)));
        }

        promotions = promotions.OrderByDescending(p => p.StartsAtUtc);

        var totalCount = await promotions.CountAsync(cancellationToken);
        var items = await promotions
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(p => p.ScopeCategory)
            .Include(p => p.ScopeBrand)
            .Include(p => p.ScopeProduct)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<PromotionDto>(items.Select(ToDto).ToList(), totalCount, query.Page, query.PageSize));
    }

    public async Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, true, cancellationToken);

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, false, cancellationToken);

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var promotion = await _dbContext.Promotions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (promotion is null)
        {
            return Result.Failure(Error.NotFound("promotion.not_found", "Promotion not found."));
        }

        _dbContext.Promotions.Remove(promotion);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var promotion = await _dbContext.Promotions.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (promotion is null)
        {
            return Result.Failure(Error.NotFound("promotion.not_found", "Promotion not found."));
        }

        promotion.IsDeleted = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PromotionApplicationDto>> FindApplicablePromotionAsync(
        string couponCode, IReadOnlyList<PromotionCartLine> lines, decimal subtotal, CancellationToken cancellationToken = default)
    {
        var normalizedCode = couponCode.Trim();
        var promotion = await _dbContext.Promotions
            .Where(p => p.CouponCode != null && p.CouponCode.ToUpper() == normalizedCode.ToUpper())
            .FirstOrDefaultAsync(cancellationToken);

        if (promotion is null)
        {
            return Result.Failure<PromotionApplicationDto>(Error.NotFound("promotion.code_not_found", "This coupon code is not valid."));
        }

        return Evaluate(promotion, lines, subtotal);
    }

    public async Task<Result<PromotionApplicationDto>> ValidateAppliedPromotionAsync(
        int promotionId, IReadOnlyList<PromotionCartLine> lines, decimal subtotal, CancellationToken cancellationToken = default)
    {
        var promotion = await _dbContext.Promotions.FirstOrDefaultAsync(p => p.Id == promotionId, cancellationToken);
        if (promotion is null)
        {
            return Result.Failure<PromotionApplicationDto>(Error.NotFound("promotion.not_found", "Promotion not found."));
        }

        return Evaluate(promotion, lines, subtotal);
    }

    private static Result<PromotionApplicationDto> Evaluate(Promotion promotion, IReadOnlyList<PromotionCartLine> lines, decimal subtotal)
    {
        if (!promotion.IsActive)
        {
            return Result.Failure<PromotionApplicationDto>(Error.Validation("promotion.inactive", "This coupon code is no longer active."));
        }

        var now = DateTime.UtcNow;
        if (now < promotion.StartsAtUtc || (promotion.EndsAtUtc.HasValue && now > promotion.EndsAtUtc.Value))
        {
            return Result.Failure<PromotionApplicationDto>(Error.Validation("promotion.not_in_window", "This coupon code has expired or is not yet active."));
        }

        if (promotion.MinimumOrderAmount.HasValue && subtotal < promotion.MinimumOrderAmount.Value)
        {
            return Result.Failure<PromotionApplicationDto>(Error.Validation(
                "promotion.minimum_not_met", $"This coupon requires a minimum order of {promotion.MinimumOrderAmount.Value:C}."));
        }

        var eligibleAmount = promotion.ScopeType switch
        {
            PromotionScopeType.EntireOrder => subtotal,
            PromotionScopeType.Category => lines.Where(l => l.CategoryId == promotion.ScopeCategoryId).Sum(l => l.LineTotal),
            PromotionScopeType.Brand => lines.Where(l => l.BrandId == promotion.ScopeBrandId).Sum(l => l.LineTotal),
            PromotionScopeType.Product => lines.Where(l => l.ProductId == promotion.ScopeProductId).Sum(l => l.LineTotal),
            _ => 0m,
        };

        if (eligibleAmount <= 0)
        {
            return Result.Failure<PromotionApplicationDto>(Error.Validation(
                "promotion.no_qualifying_items", "Your cart doesn't contain any items that qualify for this coupon."));
        }

        var rawDiscount = promotion.DiscountType == PromotionDiscountType.Percentage
            ? eligibleAmount * (promotion.DiscountValue / 100m)
            : promotion.DiscountValue;

        var cappedDiscount = promotion.MaxDiscountAmount.HasValue ? Math.Min(rawDiscount, promotion.MaxDiscountAmount.Value) : rawDiscount;
        var discountAmount = Math.Min(cappedDiscount, eligibleAmount);

        return Result.Success(new PromotionApplicationDto(promotion.Id, promotion.Name, promotion.CouponCode ?? string.Empty, discountAmount));
    }

    private async Task<bool> CouponCodeInUseAsync(string? couponCode, int? excludingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            return false;
        }

        return await _dbContext.Promotions.AnyAsync(
            p => p.CouponCode != null && p.CouponCode.ToUpper() == couponCode.ToUpper() && p.Id != excludingId, cancellationToken);
    }

    private async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var promotion = await _dbContext.Promotions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (promotion is null)
        {
            return Result.Failure(Error.NotFound("promotion.not_found", "Promotion not found."));
        }

        promotion.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<PromotionDto> ToDtoAsync(Promotion promotion, CancellationToken cancellationToken)
    {
        if (promotion.ScopeCategoryId.HasValue || promotion.ScopeBrandId.HasValue || promotion.ScopeProductId.HasValue)
        {
            await _dbContext.Entry(promotion).Reference(p => p.ScopeCategory).LoadAsync(cancellationToken);
            await _dbContext.Entry(promotion).Reference(p => p.ScopeBrand).LoadAsync(cancellationToken);
            await _dbContext.Entry(promotion).Reference(p => p.ScopeProduct).LoadAsync(cancellationToken);
        }

        return ToDto(promotion);
    }

    private static PromotionDto ToDto(Promotion promotion) => new(
        promotion.Id,
        promotion.Name,
        promotion.Description,
        promotion.CouponCode,
        promotion.DiscountType.ToString(),
        promotion.DiscountValue,
        promotion.ScopeType.ToString(),
        promotion.ScopeCategoryId,
        promotion.ScopeCategory?.Name,
        promotion.ScopeBrandId,
        promotion.ScopeBrand?.Name,
        promotion.ScopeProductId,
        promotion.ScopeProduct?.Name,
        promotion.MinimumOrderAmount,
        promotion.MaxDiscountAmount,
        promotion.StartsAtUtc,
        promotion.EndsAtUtc,
        promotion.MaxTotalUses,
        promotion.MaxUsesPerCustomer,
        promotion.IsActive,
        promotion.IsDeleted);
}
