using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Shipping;

/// <summary>
/// Admin-managed shipping method (Milestone 7.3) for a jurisdiction.
/// Unlike <see cref="Taxation.TaxRate"/> (one rate per category per
/// jurisdiction), several named methods can coexist for the same
/// jurisdiction (e.g. Standard and Express) - uniqueness is on
/// <see cref="Name"/> within a jurisdiction, not the jurisdiction alone.
/// Cost is <see cref="BaseRate"/> plus <see cref="RatePerKg"/> times the
/// order's total weight (from <see cref="Catalog.Product.Weight"/>, a field
/// that's existed unused since Milestone 2.4 - a line whose product has no
/// recorded weight contributes 0kg, the same leniency untracked inventory
/// already gets), waived entirely once <see cref="FreeShippingThreshold"/>
/// is met. There's no real customer destination to calculate against until
/// Milestone 8.1's Addresses exist - see Architecture.md's Milestone 7.3
/// section.
/// </summary>
public class ShippingMethod : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string? RegionCode { get; set; }
    public decimal BaseRate { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal? FreeShippingThreshold { get; set; }
    public int? EstimatedDeliveryDaysMin { get; set; }
    public int? EstimatedDeliveryDaysMax { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
