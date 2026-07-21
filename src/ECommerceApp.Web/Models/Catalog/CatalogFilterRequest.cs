namespace ECommerceApp.Web.Models.Catalog;

/// <summary>Binds every filter/sort/paging query-string parameter shared by all four
/// catalog listing routes, so the controller actions don't each need a dozen parameters.</summary>
public class CatalogFilterRequest
{
    public int Page { get; set; } = 1;
    public string View { get; set; } = "grid";
    public string Sort { get; set; } = "Relevance";
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? CategoryId { get; set; }
    public int? BrandId { get; set; }
    public bool InStock { get; set; }
    public bool Discounted { get; set; }
    public bool Featured { get; set; }
    public bool NewArrivals { get; set; }
    public int[] Attr { get; set; } = Array.Empty<int>();
}
