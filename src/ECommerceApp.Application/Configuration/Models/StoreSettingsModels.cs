namespace ECommerceApp.Application.Configuration.Models;

public record StoreSettingsDto(
    string StoreName,
    string Currency,
    string DefaultCountry,
    bool PricesIncludeTax,
    int RecentlyViewedMaxItems,
    string DefaultTaxCountryCode,
    string? DefaultTaxRegionCode,
    string DefaultShippingCountryCode,
    string? DefaultShippingRegionCode,
    byte[] RowVersion);

public record UpdateStoreSettingsRequest(
    string StoreName,
    string Currency,
    string DefaultCountry,
    bool PricesIncludeTax,
    int RecentlyViewedMaxItems,
    string DefaultTaxCountryCode,
    string? DefaultTaxRegionCode,
    string DefaultShippingCountryCode,
    string? DefaultShippingRegionCode,
    byte[] RowVersion);
