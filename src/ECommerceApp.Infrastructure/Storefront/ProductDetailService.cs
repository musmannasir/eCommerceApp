using ECommerceApp.Application.Pricing;
using ECommerceApp.Application.Storefront;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Application.Wishlist;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Storefront;

/// <summary>
/// Backs the public product detail page (Milestones 5.1-5.2). Queries ApplicationDbContext
/// directly, same convention as the other Storefront services - see Database-Design.md.
/// Price/discount calculation is delegated to IPricingService (the brief's "central
/// pricing service, single source of truth"), which is a pure calculator with no DB
/// dependency of its own, so injecting it here is safe and doesn't risk the kind of
/// cross-service transaction coupling avoided elsewhere (see Milestone 3.3's notes).
/// </summary>
public sealed class ProductDetailService : IProductDetailService
{
    private const int RelatedProductCount = 8;

    private readonly ApplicationDbContext _dbContext;
    private readonly IPricingService _pricingService;
    private readonly IRecommendationService _recommendationService;
    private readonly IRecentlyViewedService _recentlyViewedService;
    private readonly IWishlistService _wishlistService;

    public ProductDetailService(
        ApplicationDbContext dbContext,
        IPricingService pricingService,
        IRecommendationService recommendationService,
        IRecentlyViewedService recentlyViewedService,
        IWishlistService wishlistService)
    {
        _dbContext = dbContext;
        _pricingService = pricingService;
        _recommendationService = recommendationService;
        _recentlyViewedService = recentlyViewedService;
        _wishlistService = wishlistService;
    }

    public async Task<Result<ProductDetailDto>> GetDetailAsync(
        string slug, int? selectedVariantId, IReadOnlyList<int> selectedAttributeValueIds, string? userId = null, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Where(p => p.Slug == slug && p.IsActive && p.IsPublished)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Specifications)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues).ThenInclude(av => av.ProductAttributeValue).ThenInclude(pav => pav.ProductAttribute)
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return Result.Failure<ProductDetailDto>(Error.NotFound("product.not_found", "Product not found."));
        }

        var breadcrumbs = await BuildBreadcrumbsAsync(product.CategoryId, product.Name, cancellationToken);
        var activeVariants = product.Variants.Where(v => v.IsActive).ToList();
        var hasVariants = activeVariants.Count > 0;

        var attributeGroups = CollectAttributeGroups(activeVariants);

        ProductVariant? selectedVariant = null;
        var combinationUnavailable = false;

        if (hasVariants)
        {
            if (selectedVariantId.HasValue)
            {
                selectedVariant = activeVariants.FirstOrDefault(v => v.Id == selectedVariantId.Value);
            }
            else if (selectedAttributeValueIds.Count > 0)
            {
                var key = ProductVariant.BuildCombinationKey(selectedAttributeValueIds);
                selectedVariant = activeVariants.FirstOrDefault(v => v.CombinationKey == key);
                combinationUnavailable = selectedVariant is null;
            }

            selectedVariant ??= activeVariants.OrderBy(v => v.Id).First();
        }

        var attributes = attributeGroups.Select(group =>
        {
            var selectedValueId = selectedVariant?.AttributeValues
                .Select(av => av.ProductAttributeValue)
                .FirstOrDefault(v => v.ProductAttributeId == group.Id)?.Id;
            return new ProductDetailAttributeDto(group.Id, group.Name, selectedValueId, group.Values);
        }).ToList();

        var sku = selectedVariant?.SKU ?? product.BaseSKU;
        var price = _pricingService.Calculate(product.SellingPrice, product.CompareAtPrice, selectedVariant?.SellingPrice, selectedVariant?.CompareAtPrice);

        var images = BuildImages(product, selectedVariant?.Id);
        var (stockState, availableQuantity) = await GetStockAsync(product.Id, selectedVariant?.Id, product.LowStockThreshold, cancellationToken);
        var relatedProducts = await _recommendationService.GetRecommendationsAsync(product.Id, RelatedProductCount, cancellationToken);
        var recentlyViewed = await _recentlyViewedService.GetRecentlyViewedAsync(product.Id, cancellationToken);
        var isWishlisted = userId is not null && await _wishlistService.IsWishlistedAsync(userId, product.Id, cancellationToken);
        var variantCombinations = activeVariants
            .Select(v => new VariantCombinationDto(v.Id, v.AttributeValues.Select(av => av.ProductAttributeValueId).ToList()))
            .ToList();

        var dto = new ProductDetailDto(
            product.Id,
            product.Name,
            product.Slug,
            product.ShortDescription,
            product.FullDescription,
            product.Brand?.Name,
            product.Brand?.Slug,
            product.Category.Name,
            product.Category.Slug,
            breadcrumbs,
            sku,
            price.FinalPrice,
            price.CompareAtPrice,
            price.DiscountPercent,
            product.IsTaxable,
            product.TaxCategory,
            price.IsTaxInclusive,
            product.WarrantyInformation,
            product.ReturnEligibility,
            images,
            product.Specifications.OrderBy(s => s.DisplayOrder).Select(s => new ProductDetailSpecificationDto(s.Name, s.Value)).ToList(),
            attributes,
            variantCombinations,
            hasVariants,
            selectedVariant?.Id,
            combinationUnavailable,
            stockState,
            product.LowStockThreshold,
            availableQuantity,
            relatedProducts,
            recentlyViewed,
            isWishlisted);

        return Result.Success(dto);
    }

    public async Task<Result<VariantResolutionDto>> ResolveVariantAsync(string slug, int variantId, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Where(p => p.Slug == slug && p.IsActive && p.IsPublished)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return Result.Failure<VariantResolutionDto>(Error.NotFound("product.not_found", "Product not found."));
        }

        var variant = product.Variants.FirstOrDefault(v => v.Id == variantId && v.IsActive);
        if (variant is null)
        {
            return Result.Failure<VariantResolutionDto>(Error.Validation(
                "product.variant_unavailable", "This variant is not available for this product."));
        }

        var price = _pricingService.Calculate(product.SellingPrice, product.CompareAtPrice, variant.SellingPrice, variant.CompareAtPrice);
        var images = BuildImages(product, variant.Id);
        var (stockState, availableQuantity) = await GetStockAsync(product.Id, variant.Id, product.LowStockThreshold, cancellationToken);

        return Result.Success(new VariantResolutionDto(
            variant.Id,
            variant.SKU,
            price.FinalPrice,
            price.CompareAtPrice,
            price.DiscountPercent,
            images.Count > 0 ? images[0].Path : null,
            stockState,
            availableQuantity,
            product.LowStockThreshold));
    }

    private static List<(int Id, string Name, List<ProductDetailAttributeValueDto> Values)> CollectAttributeGroups(IReadOnlyList<ProductVariant> activeVariants)
    {
        var attributes = new Dictionary<int, (int Id, string Name, List<ProductDetailAttributeValueDto> Values)>();
        foreach (var variant in activeVariants)
        {
            foreach (var av in variant.AttributeValues)
            {
                var value = av.ProductAttributeValue;
                var attribute = value.ProductAttribute;
                if (!attributes.TryGetValue(attribute.Id, out var entry))
                {
                    entry = (attribute.Id, attribute.Name, new List<ProductDetailAttributeValueDto>());
                    attributes[attribute.Id] = entry;
                }

                if (entry.Values.All(v => v.Id != value.Id))
                {
                    entry.Values.Add(new ProductDetailAttributeValueDto(value.Id, value.Value));
                }
            }
        }

        return attributes.Values.OrderBy(a => a.Name).ToList();
    }

    private static IReadOnlyList<ProductDetailImageDto> BuildImages(Product product, int? selectedVariantId)
    {
        var variantImages = selectedVariantId.HasValue
            ? product.Images.Where(i => i.ProductVariantId == selectedVariantId.Value).ToList()
            : new List<ProductImage>();

        var images = variantImages.Count > 0 ? variantImages : product.Images.Where(i => i.ProductVariantId == null).ToList();

        return images
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.DisplayOrder)
            .Select(i => new ProductDetailImageDto(i.Path, i.AltText, i.IsPrimary))
            .ToList();
    }

    private async Task<(ProductStockState State, int Available)> GetStockAsync(
        int productId, int? variantId, int? lowStockThreshold, CancellationToken cancellationToken)
    {
        var items = variantId.HasValue
            ? await _dbContext.InventoryItems.Where(i => i.ProductVariantId == variantId.Value).ToListAsync(cancellationToken)
            : await _dbContext.InventoryItems.Where(i => i.ProductId == productId && i.ProductVariantId == null).ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            // Untracked inventory is treated as available, same lenient reasoning as the
            // out-of-stock badge on listing pages (Milestone 4) - don't punish a product
            // that simply hasn't had stock recorded yet.
            return (ProductStockState.InStock, int.MaxValue);
        }

        var onHand = items.Sum(i => i.QuantityOnHand);
        var reserved = items.Sum(i => i.QuantityReserved);
        var available = onHand - reserved;
        var allowBackorder = items.Any(i => i.AllowBackorder);

        if (available > (lowStockThreshold ?? 0))
        {
            return (ProductStockState.InStock, available);
        }

        if (available > 0)
        {
            return (ProductStockState.LowStock, available);
        }

        return (allowBackorder ? ProductStockState.Backorder : ProductStockState.OutOfStock, available);
    }

    private async Task<IReadOnlyList<BreadcrumbEntryDto>> BuildBreadcrumbsAsync(int categoryId, string productName, CancellationToken cancellationToken)
    {
        var allCategories = await _dbContext.Categories
            .Select(c => new { c.Id, c.Name, c.Slug, c.ParentCategoryId })
            .ToListAsync(cancellationToken);

        var chain = new List<BreadcrumbEntryDto> { new("Home", "/") };
        var ancestors = new Stack<(string Name, string Slug)>();
        var current = allCategories.FirstOrDefault(c => c.Id == categoryId);
        var visited = new HashSet<int>();

        while (current is not null && visited.Add(current.Id))
        {
            ancestors.Push((current.Name, current.Slug));
            current = current.ParentCategoryId.HasValue ? allCategories.FirstOrDefault(c => c.Id == current.ParentCategoryId.Value) : null;
        }

        while (ancestors.Count > 0)
        {
            var (name, slug) = ancestors.Pop();
            chain.Add(new BreadcrumbEntryDto(name, $"/Category/{slug}"));
        }

        chain.Add(new BreadcrumbEntryDto(productName, null));
        return chain;
    }
}
