using ECommerceApp.Application.Common.Models;

namespace ECommerceApp.Application.Storefront.Models;

public enum CatalogBrowseMode
{
    All,
    Category,
    Brand,
    Search,
}

/// <summary>
/// HighestRated and BestSelling are intentionally not offered - there is no rating
/// data (Milestone 12) or order/sales history (Milestone 9) yet to sort by, and a
/// sort control that silently produces arbitrary results would be misleading.
/// </summary>
public enum CatalogSortOption
{
    Relevance,
    Newest,
    PriceAsc,
    PriceDesc,
    LargestDiscount,
    NameAsc,
    NameDesc,
}

public record CatalogBrowseQuery
{
    public CatalogBrowseMode Mode { get; init; } = CatalogBrowseMode.All;
    public string? CategorySlug { get; init; }
    public string? BrandSlug { get; init; }
    public string? SearchTerm { get; init; }

    /// <summary>Additional category narrowing layered on top of Mode - e.g. picking a
    /// subcategory while on a Category page, or picking a category while searching.</summary>
    public int? FilterCategoryId { get; init; }

    /// <summary>Additional brand narrowing, used when Mode isn't already Brand.</summary>
    public int? FilterBrandId { get; init; }

    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool OnlyInStock { get; init; }
    public bool OnlyDiscounted { get; init; }
    public bool OnlyFeatured { get; init; }
    public bool OnlyNewArrivals { get; init; }
    public IReadOnlyList<int> AttributeValueIds { get; init; } = Array.Empty<int>();

    public CatalogSortOption Sort { get; init; } = CatalogSortOption.Relevance;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
}

public record CatalogFilterOptionDto(int Id, string Name, string Slug);

public record AttributeFilterValueDto(int Id, string Value);

public record AttributeFilterDto(int Id, string Name, IReadOnlyList<AttributeFilterValueDto> Values);

/// <summary>Options the filter panel needs to render - available categories/brands/attributes
/// and which of them (if any) are currently selected, so the UI can show active-filter state.
/// Subcategories is only populated in Category mode, for the current category's direct children.</summary>
public record CatalogFilterOptionsDto(
    IReadOnlyList<CatalogFilterOptionDto> Categories,
    IReadOnlyList<CatalogFilterOptionDto> Subcategories,
    IReadOnlyList<CatalogFilterOptionDto> Brands,
    IReadOnlyList<AttributeFilterDto> Attributes);

/// <summary>Null CategoryName/BrandName means "not that browse mode"; a Category/Brand
/// mode query for a slug that doesn't exist (or isn't active) fails with NotFound instead.</summary>
public record CatalogBrowseResultDto(
    PagedResult<HomeProductCardDto> Products,
    string? CategoryName,
    string? BrandName,
    CatalogFilterOptionsDto FilterOptions);

public record SearchSuggestionDto(string Name, string? ImagePath, decimal Price, string? CategoryName, string Link);
