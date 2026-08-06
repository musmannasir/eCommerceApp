using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Configuration;

/// <summary>
/// Milestone 16.3 - a singleton row (exactly one is ever seeded/read) of
/// store-wide configuration that used to live only in appsettings.json's
/// static "Store" section. Derives from <see cref="AuditableEntity"/>
/// (rather than a plain <see cref="BaseEntity"/>) specifically for its
/// <see cref="IHasRowVersion.RowVersion"/> - two admins editing this same
/// shared row at once is a real possibility ordinary per-record entities
/// don't have to worry about, so optimistic concurrency matters here more
/// than usual. <see cref="IsDeleted"/> is inherited but never meaningfully
/// used - there is no "recycle bin" for the one settings row.
/// </summary>
public class StoreSettings : AuditableEntity
{
    public string StoreName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string DefaultCountry { get; set; } = string.Empty;
    public bool PricesIncludeTax { get; set; }
    public int RecentlyViewedMaxItems { get; set; }
    public string DefaultTaxCountryCode { get; set; } = string.Empty;
    public string? DefaultTaxRegionCode { get; set; }
    public string DefaultShippingCountryCode { get; set; } = string.Empty;
    public string? DefaultShippingRegionCode { get; set; }
}
