namespace ECommerceApp.Application.Shipping.Models;

public record ShippingMethodDto(
    int Id,
    string Name,
    string? Description,
    string CountryCode,
    string? RegionCode,
    decimal BaseRate,
    decimal RatePerKg,
    decimal? FreeShippingThreshold,
    int? EstimatedDeliveryDaysMin,
    int? EstimatedDeliveryDaysMax,
    int DisplayOrder,
    bool IsActive,
    bool IsDeleted);

public record CreateShippingMethodRequest(
    string Name,
    string? Description,
    string CountryCode,
    string? RegionCode,
    decimal BaseRate,
    decimal RatePerKg,
    decimal? FreeShippingThreshold,
    int? EstimatedDeliveryDaysMin,
    int? EstimatedDeliveryDaysMax,
    int DisplayOrder,
    bool IsActive);

public record UpdateShippingMethodRequest(
    int Id,
    string Name,
    string? Description,
    string CountryCode,
    string? RegionCode,
    decimal BaseRate,
    decimal RatePerKg,
    decimal? FreeShippingThreshold,
    int? EstimatedDeliveryDaysMin,
    int? EstimatedDeliveryDaysMax,
    int DisplayOrder,
    bool IsActive);

/// <summary>A computed cost for one shipping method against a specific order snapshot - not a database row.</summary>
public record ShippingOptionDto(
    int ShippingMethodId,
    string Name,
    string? Description,
    decimal Cost,
    int? EstimatedDeliveryDaysMin,
    int? EstimatedDeliveryDaysMax);

/// <summary>
/// RateConfigured is false when no active method exists at all for the
/// destination - distinct from a genuine free/zero-cost method, mirroring
/// TaxCalculationResult's RateConfigured signal.
/// </summary>
public record EstimatedShippingResult(decimal Cost, bool RateConfigured);
