using System.Linq.Expressions;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Storefront;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Storefront;

/// <summary>
/// Backs the public category/brand/search/all-products listing pages
/// (Milestones 4.2-4.3). Queries ApplicationDbContext directly, same convention
/// as HomePageService - see Database-Design.md.
/// </summary>
public sealed class CatalogBrowseService : ICatalogBrowseService
{
    private const int SuggestionCount = 8;
    private const int NewArrivalWindowDays = 30;

    private readonly ApplicationDbContext _dbContext;

    public CatalogBrowseService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<CatalogBrowseResultDto>> BrowseAsync(CatalogBrowseQuery query, CancellationToken cancellationToken = default)
    {
        // A customer must never see a draft/inactive product, regardless of browse mode.
        var sellableProducts = _dbContext.Products.Where(p => p.IsActive && p.IsPublished);
        string? categoryName = null;
        string? brandName = null;
        int? scopedCategoryId = null;

        switch (query.Mode)
        {
            case CatalogBrowseMode.Category:
                var category = await _dbContext.Categories
                    .FirstOrDefaultAsync(c => c.Slug == query.CategorySlug && c.IsActive, cancellationToken);
                if (category is null)
                {
                    return Result.Failure<CatalogBrowseResultDto>(Error.NotFound("catalog.category_not_found", "Category not found."));
                }

                categoryName = category.Name;
                scopedCategoryId = category.Id;
                break;

            case CatalogBrowseMode.Brand:
                var brand = await _dbContext.Brands
                    .FirstOrDefaultAsync(b => b.Slug == query.BrandSlug && b.IsActive, cancellationToken);
                if (brand is null)
                {
                    return Result.Failure<CatalogBrowseResultDto>(Error.NotFound("catalog.brand_not_found", "Brand not found."));
                }

                sellableProducts = sellableProducts.Where(p => p.BrandId == brand.Id);
                brandName = brand.Name;
                break;

            case CatalogBrowseMode.Search:
                sellableProducts = ApplySearchTerm(sellableProducts, query.SearchTerm);
                break;

            case CatalogBrowseMode.All:
            default:
                break;
        }

        // A picked subcategory narrows further than (and supersedes) the page's own
        // category scope - its descendant set is already a subset of the parent's.
        var effectiveCategoryId = query.FilterCategoryId ?? scopedCategoryId;
        if (effectiveCategoryId.HasValue)
        {
            var categoryIds = await GetActiveDescendantCategoryIdsAsync(effectiveCategoryId.Value, cancellationToken);
            sellableProducts = sellableProducts.Where(p => categoryIds.Contains(p.CategoryId));
        }

        if (query.FilterBrandId.HasValue && query.Mode != CatalogBrowseMode.Brand)
        {
            sellableProducts = sellableProducts.Where(p => p.BrandId == query.FilterBrandId.Value);
        }

        if (query.MinPrice.HasValue)
        {
            sellableProducts = sellableProducts.Where(p => p.SellingPrice >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            sellableProducts = sellableProducts.Where(p => p.SellingPrice <= query.MaxPrice.Value);
        }

        if (query.OnlyFeatured)
        {
            sellableProducts = sellableProducts.Where(p => p.IsFeatured);
        }

        if (query.OnlyDiscounted)
        {
            sellableProducts = sellableProducts.Where(p => p.CompareAtPrice != null && p.CompareAtPrice > p.SellingPrice);
        }

        if (query.OnlyNewArrivals)
        {
            var cutoff = DateTime.UtcNow.AddDays(-NewArrivalWindowDays);
            sellableProducts = sellableProducts.Where(p => (p.PublishedAtUtc ?? p.CreatedAtUtc) >= cutoff);
        }

        if (query.OnlyInStock)
        {
            sellableProducts = sellableProducts.Where(p =>
                !_dbContext.InventoryItems.Any(i => i.ProductId == p.Id) ||
                _dbContext.InventoryItems.Any(i => i.ProductId == p.Id && (i.QuantityOnHand - i.QuantityReserved > 0 || i.AllowBackorder)));
        }

        if (query.AttributeValueIds.Count > 0)
        {
            sellableProducts = await ApplyAttributeFiltersAsync(sellableProducts, query.AttributeValueIds, cancellationToken);
        }

        sellableProducts = ApplySort(sellableProducts, query.Sort, query.SearchTerm);

        var totalCount = await sellableProducts.CountAsync(cancellationToken);
        var items = await sellableProducts
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .Select(CardProjection())
            .ToListAsync(cancellationToken);

        var page = new PagedResult<HomeProductCardDto>(items, totalCount, query.Page, query.PageSize);
        var filterOptions = await GetFilterOptionsAsync(scopedCategoryId, cancellationToken);
        return Result.Success(new CatalogBrowseResultDto(page, categoryName, brandName, filterOptions));
    }

    public async Task<IReadOnlyList<SearchSuggestionDto>> GetSuggestionsAsync(string term, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Array.Empty<SearchSuggestionDto>();
        }

        var matches = ApplySearchTerm(_dbContext.Products.Where(p => p.IsActive && p.IsPublished), term);
        matches = ApplySort(matches, CatalogSortOption.Relevance, term);

        return await matches
            .Take(SuggestionCount)
            .AsNoTracking()
            .Select(p => new SearchSuggestionDto(
                p.Name,
                p.Images.Where(i => i.IsPrimary).Select(i => i.Path).FirstOrDefault() ?? p.Images.Select(i => i.Path).FirstOrDefault(),
                p.SellingPrice,
                p.Category.Name,
                "/Search?q=" + p.Name))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Baseline substring matching across name/SKU/brand/category/tags/keywords/short
    /// description, per the brief - no external search engine, no relevance ranking beyond
    /// what ApplySort adds. Normalizes by trimming; SQL Server's default collation is
    /// case-insensitive, so no explicit casing normalization is needed.</summary>
    private static IQueryable<Product> ApplySearchTerm(IQueryable<Product> products, string? term)
    {
        var trimmed = term?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return products;
        }

        return products.Where(p =>
            p.Name.Contains(trimmed) ||
            p.BaseSKU.Contains(trimmed) ||
            (p.ShortDescription != null && p.ShortDescription.Contains(trimmed)) ||
            (p.SearchKeywords != null && p.SearchKeywords.Contains(trimmed)) ||
            (p.Brand != null && p.Brand.Name.Contains(trimmed)) ||
            p.Category.Name.Contains(trimmed) ||
            p.TagMappings.Any(tm => tm.ProductTag.Name.Contains(trimmed)));
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> products, CatalogSortOption sort, string? searchTerm) => sort switch
    {
        CatalogSortOption.Newest => products.OrderByDescending(p => p.PublishedAtUtc ?? p.CreatedAtUtc),
        CatalogSortOption.PriceAsc => products.OrderBy(p => p.SellingPrice),
        CatalogSortOption.PriceDesc => products.OrderByDescending(p => p.SellingPrice),
        CatalogSortOption.LargestDiscount => products.OrderByDescending(p => p.CompareAtPrice != null && p.CompareAtPrice > p.SellingPrice ? p.CompareAtPrice.Value - p.SellingPrice : 0),
        CatalogSortOption.NameDesc => products.OrderByDescending(p => p.Name),
        CatalogSortOption.NameAsc => products.OrderBy(p => p.Name),
        CatalogSortOption.Relevance when !string.IsNullOrWhiteSpace(searchTerm) => products
            .OrderByDescending(p => p.Name.StartsWith(searchTerm!))
            .ThenByDescending(p => p.Name.Contains(searchTerm!))
            .ThenBy(p => p.Name),
        _ => products.OrderBy(p => p.Name),
    };

    /// <summary>Faceted semantics: a selected value from attribute A and one from attribute B
    /// must both match (AND across attributes), but any selected value within the same
    /// attribute is enough (OR within an attribute) - the standard e-commerce filter shape.</summary>
    private async Task<IQueryable<Product>> ApplyAttributeFiltersAsync(IQueryable<Product> products, IReadOnlyList<int> attributeValueIds, CancellationToken cancellationToken)
    {
        var valueGroups = await _dbContext.ProductAttributeValues
            .Where(v => attributeValueIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductAttributeId })
            .ToListAsync(cancellationToken);

        foreach (var group in valueGroups.GroupBy(v => v.ProductAttributeId))
        {
            var idsInGroup = group.Select(v => v.Id).ToList();
            products = products.Where(p => p.Variants.Any(variant =>
                variant.AttributeValues.Any(av => idsInGroup.Contains(av.ProductAttributeValueId))));
        }

        return products;
    }

    private async Task<CatalogFilterOptionsDto> GetFilterOptionsAsync(int? scopedCategoryId, CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories
            .Where(c => c.IsActive && c.ParentCategoryId == null)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CatalogFilterOptionDto(c.Id, c.Name, c.Slug))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var subcategories = scopedCategoryId.HasValue
            ? await _dbContext.Categories
                .Where(c => c.IsActive && c.ParentCategoryId == scopedCategoryId.Value)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new CatalogFilterOptionDto(c.Id, c.Name, c.Slug))
                .AsNoTracking()
                .ToListAsync(cancellationToken)
            : new List<CatalogFilterOptionDto>();

        var brands = await _dbContext.Brands
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new CatalogFilterOptionDto(b.Id, b.Name, b.Slug))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var attributes = await _dbContext.ProductAttributes
            .OrderBy(a => a.Name)
            .Select(a => new AttributeFilterDto(
                a.Id,
                a.Name,
                a.Values.OrderBy(v => v.Value).Select(v => new AttributeFilterValueDto(v.Id, v.Value)).ToList()))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new CatalogFilterOptionsDto(categories, subcategories, brands, attributes);
    }

    /// <summary>Categories table is small; loading all active categories once and
    /// walking parent/child links in memory is simpler than a recursive SQL CTE.</summary>
    private async Task<HashSet<int>> GetActiveDescendantCategoryIdsAsync(int rootId, CancellationToken cancellationToken)
    {
        var activeCategories = await _dbContext.Categories
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.ParentCategoryId })
            .ToListAsync(cancellationToken);

        var result = new HashSet<int> { rootId };
        var frontier = new Queue<int>();
        frontier.Enqueue(rootId);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var child in activeCategories.Where(c => c.ParentCategoryId == current))
            {
                if (result.Add(child.Id))
                {
                    frontier.Enqueue(child.Id);
                }
            }
        }

        return result;
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
