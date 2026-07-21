using System.Linq.Expressions;
using ECommerceApp.Application.Storefront;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Storefront;

/// <summary>
/// "Recommendations v1" (Milestone 5.3) - see IRecommendationService for why
/// "best selling" isn't one of the signals yet. Runs in two passes: first
/// score/order/take candidate IDs (a lean projection, safe to compute the
/// score inline since it's all arithmetic on scalar columns), then re-query
/// those specific IDs through the same inline card-projection Expression
/// every other Storefront service uses, and finally re-sort the results to
/// match the score order (Contains() doesn't guarantee it).
/// </summary>
public sealed class RecommendationService : IRecommendationService
{
    private const decimal PriceRangeTolerance = 0.3m;

    private readonly ApplicationDbContext _dbContext;

    public RecommendationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<HomeProductCardDto>> GetRecommendationsAsync(int productId, int count, CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.Products
            .Where(p => p.Id == productId)
            .Select(p => new
            {
                p.CategoryId,
                p.BrandId,
                p.SellingPrice,
                TagIds = p.TagMappings.Select(tm => tm.ProductTagId).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (source is null)
        {
            return Array.Empty<HomeProductCardDto>();
        }

        var minPrice = source.SellingPrice * (1 - PriceRangeTolerance);
        var maxPrice = source.SellingPrice * (1 + PriceRangeTolerance);

        var scored = await _dbContext.Products
            .Where(p => p.IsActive && p.IsPublished && p.Id != productId)
            .Select(p => new
            {
                p.Id,
                Score =
                    (p.CategoryId == source.CategoryId ? 3 : 0) +
                    (source.BrandId != null && p.BrandId == source.BrandId ? 2 : 0) +
                    (p.SellingPrice >= minPrice && p.SellingPrice <= maxPrice ? 1 : 0) +
                    p.TagMappings.Count(tm => source.TagIds.Contains(tm.ProductTagId)),
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

        if (scored.Count == 0)
        {
            return Array.Empty<HomeProductCardDto>();
        }

        var ids = scored.Select(s => s.Id).ToList();
        var cards = await _dbContext.Products
            .Where(p => ids.Contains(p.Id))
            .AsNoTracking()
            .Select(CardProjection())
            .ToListAsync(cancellationToken);

        var cardsById = cards.ToDictionary(c => c.Id);
        return ids.Where(cardsById.ContainsKey).Select(id => cardsById[id]).ToList();
    }

    private Expression<Func<Product, HomeProductCardDto>> CardProjection() => p => new HomeProductCardDto(
        p.Id,
        p.Name,
        p.Slug,
        p.Images.Where(i => i.IsPrimary).Select(i => i.Path).FirstOrDefault() ?? p.Images.Select(i => i.Path).FirstOrDefault(),
        p.Brand != null ? p.Brand.Name : null,
        p.Brand != null ? p.Brand.Slug : null,
        p.SellingPrice,
        p.CompareAtPrice,
        _dbContext.InventoryItems.Any(i => i.ProductId == p.Id) &&
            !_dbContext.InventoryItems.Any(i => i.ProductId == p.Id && (i.QuantityOnHand - i.QuantityReserved > 0 || i.AllowBackorder)));
}
