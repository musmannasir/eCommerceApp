using ECommerceApp.Application.Pricing.Models;

namespace ECommerceApp.Application.Pricing;

/// <summary>
/// Pure calculation over caller-supplied base/variant values - no promotion/tax
/// lookups here (Milestones 7.1/7.2). Async since Milestone 16.3: it reads
/// IsTaxInclusive from the admin-editable, cached store settings rather than
/// static configuration.
/// </summary>
public interface IPricingService
{
    Task<PriceResultDto> CalculateAsync(decimal basePrice, decimal? baseCompareAtPrice, decimal? variantPrice, decimal? variantCompareAtPrice, CancellationToken cancellationToken = default);
}
