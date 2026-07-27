using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Taxation.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Taxation;

public interface ITaxService
{
    Task<Result<TaxRateDto>> CreateAsync(CreateTaxRateRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaxRateDto>> UpdateAsync(UpdateTaxRateRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaxRateDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<TaxRateDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up the applicable active rate for a destination + tax category:
    /// an exact (CountryCode, RegionCode, TaxCategory) match takes
    /// precedence, falling back to a country-wide (RegionCode null) rate for
    /// the same CountryCode/TaxCategory. Matching is case-insensitive.
    /// Never fails - an unconfigured jurisdiction returns a zero-tax result
    /// with RateConfigured false rather than an error, since "no rate set
    /// yet" is an expected admin-configuration state, not a fault.
    /// </summary>
    Task<TaxCalculationResult> CalculateTaxAsync(
        decimal taxableAmount, string taxCategory, string countryCode, string? regionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience wrapper for Cart's "estimated tax" display (Milestone
    /// 7.2): calculates and sums tax per line against the store's
    /// configured default jurisdiction (Store:DefaultTaxCountryCode/
    /// RegionCode) rather than a real customer destination - there's no
    /// Address entity to derive one from until Milestone 8.1. Callers pass
    /// only taxable, available lines; RateConfigured is true if at least
    /// one line's category had a configured rate.
    /// </summary>
    Task<EstimatedTaxResult> CalculateEstimatedTaxAsync(IReadOnlyList<TaxableLine> lines, CancellationToken cancellationToken = default);
}
