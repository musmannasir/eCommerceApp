namespace ECommerceApp.Application.Taxation.Models;

public record TaxRateDto(
    int Id,
    string CountryCode,
    string? RegionCode,
    string TaxCategory,
    decimal RatePercent,
    bool IsActive,
    bool IsDeleted);

public record CreateTaxRateRequest(
    string CountryCode,
    string? RegionCode,
    string TaxCategory,
    decimal RatePercent,
    bool IsActive);

public record UpdateTaxRateRequest(
    int Id,
    string CountryCode,
    string? RegionCode,
    string TaxCategory,
    decimal RatePercent,
    bool IsActive);

/// <summary>
/// Lean per-line input for tax estimation - decoupled from Cart/Product's
/// domain model, mirroring how IPricingService/IPromotionService take raw
/// scalars instead of entities. Amount should already exclude any
/// non-taxable or unavailable lines - the caller decides that, same as
/// PromotionCartLine's caller does.
/// </summary>
public record TaxableLine(decimal Amount, string TaxCategory);

/// <summary>
/// RateConfigured is false when no matching TaxRate row exists at all for
/// the destination - distinct from a genuine 0% rate, so a caller can
/// choose to hide an "estimated tax" line entirely rather than show a
/// possibly-misleading $0.00 for a jurisdiction nobody has configured yet.
/// </summary>
public record TaxCalculationResult(decimal TaxAmount, decimal RatePercent, bool RateConfigured);

public record EstimatedTaxResult(decimal TaxAmount, bool RateConfigured);
