using ECommerceApp.Application.Storefront;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Marketing;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Storefront;

/// <summary>
/// Queries ApplicationDbContext directly rather than composing through
/// ICategoryService/IProductService/IHomePageBannerService, matching this
/// solution's established convention (see Database-Design.md) of each service
/// owning its own DbContext access rather than calling into other services.
/// </summary>
public sealed class HomePageService : IHomePageService
{
    private const int SectionSize = 8;
    private const int FeaturedCategoryCount = 6;

    private readonly ApplicationDbContext _dbContext;

    public HomePageService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HomePageDto> GetHomePageAsync(CancellationToken cancellationToken = default)
    {
        // A banner with no image uploaded yet isn't ready for the storefront -
        // the Admin edit page warns about this explicitly; enforce it here too.
        var heroBanners = await _dbContext.HomePageBanners
            .Where(b => b.IsActive && b.BannerType == BannerType.Hero && b.ImagePath != null)
            .OrderBy(b => b.DisplayOrder)
            .AsNoTracking()
            .Select(b => new HomeBannerDto(b.Id, b.Title, b.Subtitle, b.ImagePath, b.LinkUrl))
            .ToListAsync(cancellationToken);

        var promoBanners = await _dbContext.HomePageBanners
            .Where(b => b.IsActive && b.BannerType == BannerType.Promo && b.ImagePath != null)
            .OrderBy(b => b.DisplayOrder)
            .AsNoTracking()
            .Select(b => new HomeBannerDto(b.Id, b.Title, b.Subtitle, b.ImagePath, b.LinkUrl))
            .ToListAsync(cancellationToken);

        var featuredCategories = await _dbContext.Categories
            .Where(c => c.IsActive && c.IsFeatured)
            .OrderBy(c => c.DisplayOrder)
            .Take(FeaturedCategoryCount)
            .AsNoTracking()
            .Select(c => new HomeCategoryCardDto(c.Id, c.Name, c.Slug, c.ImagePath))
            .ToListAsync(cancellationToken);

        // A customer must never see a draft/inactive product, so every product
        // query below filters IsActive && IsPublished regardless of section.
        var sellableProducts = _dbContext.Products.Where(p => p.IsActive && p.IsPublished);

        var featuredProducts = await sellableProducts
            .Where(p => p.IsFeatured)
            .OrderBy(p => p.Name)
            .Take(SectionSize)
            .AsNoTracking()
            .Select(CardProjection())
            .ToListAsync(cancellationToken);

        var newArrivals = await sellableProducts
            .OrderByDescending(p => p.PublishedAtUtc ?? p.CreatedAtUtc)
            .Take(SectionSize)
            .AsNoTracking()
            .Select(CardProjection())
            .ToListAsync(cancellationToken);

        var discountedProducts = await sellableProducts
            .Where(p => p.CompareAtPrice != null && p.CompareAtPrice > p.SellingPrice)
            .OrderByDescending(p => p.CompareAtPrice! - p.SellingPrice)
            .Take(SectionSize)
            .AsNoTracking()
            .Select(CardProjection())
            .ToListAsync(cancellationToken);

        return new HomePageDto(heroBanners, promoBanners, featuredCategories, featuredProducts, newArrivals, discountedProducts);
    }

    /// <summary>
    /// Builds the product-card projection inline (not via a separate static
    /// helper method) because EF Core cannot translate an arbitrary method
    /// call inside Select() into SQL - only expression trees it can walk
    /// itself (property access, `new`, and standard LINQ subqueries like
    /// Where/Select/FirstOrDefault/Any) are guaranteed to translate. This
    /// mirrors the "InMemory passed but the real provider wouldn't have"
    /// class of bug documented in Architecture.md for Milestone 2.
    /// </summary>
    private System.Linq.Expressions.Expression<Func<Domain.Catalog.Product, HomeProductCardDto>> CardProjection() => p => new HomeProductCardDto(
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
