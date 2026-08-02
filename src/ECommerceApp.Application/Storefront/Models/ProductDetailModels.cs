using System.Text.Json.Serialization;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Reviews.Models;

namespace ECommerceApp.Application.Storefront.Models;

public record BreadcrumbEntryDto(string Text, string? Url);

public record ProductDetailImageDto(string Path, string? AltText, bool IsPrimary);

public record ProductDetailSpecificationDto(string Name, string Value);

public record ProductDetailAttributeValueDto(int Id, string Value);

/// <summary>SelectedValueId is null if the customer hasn't picked a value for this attribute yet.</summary>
public record ProductDetailAttributeDto(int Id, string Name, int? SelectedValueId, IReadOnlyList<ProductDetailAttributeValueDto> Values);

/// <summary>One active variant's full attribute-value combination - sent to the client so it
/// can disable incompatible dropdown options without a round trip per hover (Milestone 5.2).</summary>
public record VariantCombinationDto(int VariantId, IReadOnlyList<int> AttributeValueIds);

/// <summary>Server-authoritative response for the live (AJAX, no-reload) variant resolution
/// endpoint - deliberately strict: unlike the page-load resolution below, this rejects a
/// variant that doesn't exist, isn't active, or doesn't belong to the product, instead of
/// silently falling back. The client-side matrix should never let a customer construct an
/// invalid combination in the first place, so reaching one here means something bypassed it.</summary>
public record VariantResolutionDto(
    int VariantId,
    string Sku,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    int? DiscountPercent,
    string? ImagePath,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ProductStockState StockState,
    int AvailableQuantity,
    int? LowStockThreshold);

public enum ProductStockState
{
    InStock,
    LowStock,
    OutOfStock,
    Backorder,
}

/// <summary>
/// Everything the product detail page needs in one call. Page-load variant resolution
/// (query-string driven) is lenient - an unmatched combination falls back to the first
/// active variant with a notice, since the URL could be an arbitrary bookmark. Live,
/// no-reload switching after the page has loaded goes through IProductDetailService
/// .ResolveVariantAsync instead, which is strict (Milestone 5.2).
/// </summary>
public record ProductDetailDto(
    int Id,
    string Name,
    string Slug,
    string? ShortDescription,
    string? FullDescription,
    string? BrandName,
    string? BrandSlug,
    string CategoryName,
    string CategorySlug,
    IReadOnlyList<BreadcrumbEntryDto> Breadcrumbs,
    string Sku,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    int? DiscountPercent,
    bool IsTaxable,
    string TaxCategory,
    bool IsTaxInclusive,
    string? WarrantyInformation,
    string? ReturnEligibility,
    IReadOnlyList<ProductDetailImageDto> Images,
    IReadOnlyList<ProductDetailSpecificationDto> Specifications,
    IReadOnlyList<ProductDetailAttributeDto> Attributes,
    IReadOnlyList<VariantCombinationDto> VariantCombinations,
    bool HasVariants,
    int? SelectedVariantId,
    bool SelectedCombinationUnavailable,
    ProductStockState StockState,
    int? LowStockThreshold,
    int AvailableQuantity,
    IReadOnlyList<HomeProductCardDto> RelatedProducts,
    IReadOnlyList<HomeProductCardDto> RecentlyViewed,
    bool IsWishlisted,
    ProductRatingSummaryDto RatingSummary,
    PagedResult<ReviewDto> Reviews,
    bool HasReviewed);
