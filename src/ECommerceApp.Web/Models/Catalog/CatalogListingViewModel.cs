using System.Text;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Storefront.Models;

namespace ECommerceApp.Web.Models.Catalog;

public class CatalogListingViewModel
{
    public PagedResult<HomeProductCardDto> Products { get; set; } = null!;
    public CatalogFilterOptionsDto FilterOptions { get; set; } = null!;
    public string ViewMode { get; set; } = "grid";
    public string Sort { get; set; } = "Relevance";
    public string PageTitle { get; set; } = "Products";
    public string? ActiveFilterLabel { get; set; }
    public string? ClearFiltersUrl { get; set; }
    public bool HasAdditionalFilters { get; set; }
    public string ClearAdditionalFiltersUrl { get; set; } = "/Products";
    public string? SearchTerm { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? SelectedCategoryId { get; set; }
    public int? SelectedBrandId { get; set; }
    public IReadOnlyList<int> SelectedAttributeValueIds { get; set; } = Array.Empty<int>();
    public bool OnlyInStock { get; set; }
    public bool OnlyDiscounted { get; set; }
    public bool OnlyFeatured { get; set; }
    public bool OnlyNewArrivals { get; set; }

    /// <summary>The page's own base path (e.g. "/Category/electronics") - the one thing every
    /// generated link (pagination, sort, view toggle) needs and can't be derived from filter state.</summary>
    public string BaseUrl { get; set; } = "/Products";

    /// <summary>Builds a link preserving every current filter, overriding only the args passed in -
    /// used for pagination, sort, and grid/list toggle links so none of them silently drop a filter.</summary>
    public string BuildUrl(int? page = null, string? sort = null, string? view = null)
    {
        var query = new StringBuilder();
        void Add(string key, string value)
        {
            query.Append(query.Length == 0 ? '?' : '&').Append(key).Append('=').Append(Uri.EscapeDataString(value));
        }

        Add("page", (page ?? Products.Page).ToString());
        Add("view", view ?? ViewMode);
        Add("sort", sort ?? Sort);
        if (!string.IsNullOrEmpty(SearchTerm)) Add("q", SearchTerm);
        if (MinPrice.HasValue) Add("minPrice", MinPrice.Value.ToString());
        if (MaxPrice.HasValue) Add("maxPrice", MaxPrice.Value.ToString());
        if (SelectedCategoryId.HasValue) Add("categoryId", SelectedCategoryId.Value.ToString());
        if (SelectedBrandId.HasValue) Add("brandId", SelectedBrandId.Value.ToString());
        if (OnlyInStock) Add("inStock", "true");
        if (OnlyDiscounted) Add("discounted", "true");
        if (OnlyFeatured) Add("featured", "true");
        if (OnlyNewArrivals) Add("newArrivals", "true");
        foreach (var id in SelectedAttributeValueIds)
        {
            query.Append(query.Length == 0 ? '?' : '&').Append("attr=").Append(id);
        }

        return BaseUrl + query;
    }
}
