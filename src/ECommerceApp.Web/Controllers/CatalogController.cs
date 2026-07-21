using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Storefront;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Web.Models.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers;

/// <summary>
/// Public category/brand/search/all-products listing pages (Milestones 4.2-4.3).
/// Product cards stay non-clickable here too - product detail pages are
/// Milestone 5's scope, same reasoning as the home page's cards.
/// </summary>
public class CatalogController : Controller
{
    private const int PageSize = 12;

    private readonly ICatalogBrowseService _browseService;
    private readonly IBrandService _brandService;

    public CatalogController(ICatalogBrowseService browseService, IBrandService brandService)
    {
        _browseService = browseService;
        _brandService = brandService;
    }

    [HttpGet("Products")]
    public async Task<IActionResult> Index([FromQuery] CatalogFilterRequest filters)
    {
        var query = BuildQuery(filters, CatalogBrowseMode.All);
        var result = await _browseService.BrowseAsync(query);

        return View("Index", BuildViewModel(result.Value, filters, "All Products", null, null, "/Products"));
    }

    [HttpGet("Category/{slug}")]
    public async Task<IActionResult> Category(string slug, [FromQuery] CatalogFilterRequest filters)
    {
        var query = BuildQuery(filters, CatalogBrowseMode.Category) with { CategorySlug = slug };
        var result = await _browseService.BrowseAsync(query);

        if (result.IsFailure)
        {
            return NotFound();
        }

        SetBreadcrumbs(("Home", "/"), (result.Value.CategoryName!, null));

        return View("Index", BuildViewModel(
            result.Value, filters, result.Value.CategoryName!, $"Category: {result.Value.CategoryName}", null, $"/Category/{slug}"));
    }

    [HttpGet("Brand/{slug}")]
    public async Task<IActionResult> Brand(string slug, [FromQuery] CatalogFilterRequest filters)
    {
        var query = BuildQuery(filters, CatalogBrowseMode.Brand) with { BrandSlug = slug };
        var result = await _browseService.BrowseAsync(query);

        if (result.IsFailure)
        {
            return NotFound();
        }

        SetBreadcrumbs(("Home", "/"), (result.Value.BrandName!, null));

        return View("Index", BuildViewModel(
            result.Value, filters, result.Value.BrandName!, $"Brand: {result.Value.BrandName}", null, $"/Brand/{slug}"));
    }

    [HttpGet("Search")]
    public async Task<IActionResult> Search(string? q, [FromQuery] CatalogFilterRequest filters)
    {
        var query = BuildQuery(filters, CatalogBrowseMode.Search) with { SearchTerm = q };
        var result = await _browseService.BrowseAsync(query);

        var title = string.IsNullOrWhiteSpace(q) ? "Search" : $"Search results for \"{q}\"";
        var filterLabel = string.IsNullOrWhiteSpace(q) ? null : $"Search: \"{q}\"";

        return View("Index", BuildViewModel(result.Value, filters, title, filterLabel, q, "/Search"));
    }

    [HttpGet("Search/Suggestions")]
    public async Task<IActionResult> Suggestions(string? q)
    {
        var suggestions = await _browseService.GetSuggestionsAsync(q ?? string.Empty);
        return Json(suggestions);
    }

    [HttpGet("Brands")]
    public async Task<IActionResult> Brands()
    {
        var result = await _brandService.GetAllActiveAsync();
        return View(result.Value);
    }

    private static CatalogBrowseQuery BuildQuery(CatalogFilterRequest filters, CatalogBrowseMode mode)
    {
        var sort = Enum.TryParse<CatalogSortOption>(filters.Sort, ignoreCase: true, out var parsedSort)
            ? parsedSort
            : CatalogSortOption.Relevance;

        return new CatalogBrowseQuery
        {
            Mode = mode,
            Page = filters.Page < 1 ? 1 : filters.Page,
            PageSize = PageSize,
            MinPrice = filters.MinPrice,
            MaxPrice = filters.MaxPrice,
            FilterCategoryId = filters.CategoryId,
            FilterBrandId = filters.BrandId,
            OnlyInStock = filters.InStock,
            OnlyDiscounted = filters.Discounted,
            OnlyFeatured = filters.Featured,
            OnlyNewArrivals = filters.NewArrivals,
            AttributeValueIds = filters.Attr,
            Sort = sort,
        };
    }

    private static CatalogListingViewModel BuildViewModel(
        CatalogBrowseResultDto result, CatalogFilterRequest filters, string pageTitle, string? activeFilterLabel, string? searchTerm, string baseUrl)
    {
        var hasAdditionalFilters = filters.MinPrice.HasValue || filters.MaxPrice.HasValue || filters.CategoryId.HasValue ||
            filters.BrandId.HasValue || filters.InStock || filters.Discounted || filters.Featured || filters.NewArrivals || filters.Attr.Length > 0;

        return new CatalogListingViewModel
        {
            Products = result.Products,
            FilterOptions = result.FilterOptions,
            ViewMode = filters.View == "list" ? "list" : "grid",
            Sort = Enum.TryParse<CatalogSortOption>(filters.Sort, true, out var sort) ? sort.ToString() : nameof(CatalogSortOption.Relevance),
            PageTitle = pageTitle,
            ActiveFilterLabel = activeFilterLabel,
            ClearFiltersUrl = activeFilterLabel is null ? null : "/Products",
            HasAdditionalFilters = hasAdditionalFilters,
            ClearAdditionalFiltersUrl = baseUrl + (searchTerm is not null ? $"?q={Uri.EscapeDataString(searchTerm)}" : string.Empty),
            SearchTerm = searchTerm,
            MinPrice = filters.MinPrice,
            MaxPrice = filters.MaxPrice,
            SelectedCategoryId = filters.CategoryId,
            SelectedBrandId = filters.BrandId,
            SelectedAttributeValueIds = filters.Attr,
            OnlyInStock = filters.InStock,
            OnlyDiscounted = filters.Discounted,
            OnlyFeatured = filters.Featured,
            OnlyNewArrivals = filters.NewArrivals,
            BaseUrl = baseUrl,
        };
    }

    private void SetBreadcrumbs(params (string Text, string? Url)[] crumbs) =>
        ViewData["Breadcrumbs"] = crumbs.ToList();
}
