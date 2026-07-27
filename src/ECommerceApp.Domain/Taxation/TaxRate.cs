using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Taxation;

/// <summary>
/// Admin-managed tax rate (Milestone 7.2) for a jurisdiction + product tax
/// category. <see cref="RegionCode"/> is null for a country-wide rate;
/// when set (e.g. a US state), it takes precedence over any country-wide
/// rate for the same <see cref="CountryCode"/>/<see cref="TaxCategory"/> -
/// see ITaxService's lookup order. <see cref="TaxCategory"/> is matched
/// against <see cref="Catalog.Product.TaxCategory"/> by plain string
/// equality (case-insensitive), not a shared FK or enum - both stay
/// free-text per the Data-Dictionary's note that a structured tax-category
/// model doesn't exist yet. There's no real customer destination to
/// calculate against until Milestone 8.1's Addresses exist, so this rate
/// table is consumed today only as an estimate against the store's
/// configured default jurisdiction (Store:DefaultTaxCountryCode/
/// RegionCode) - see Architecture.md's Milestone 7.2 section.
/// </summary>
public class TaxRate : AuditableEntity
{
    public string CountryCode { get; set; } = string.Empty;
    public string? RegionCode { get; set; }
    public string TaxCategory { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public bool IsActive { get; set; } = true;
}
