using ECommerceApp.Application.Pricing.Models;

namespace ECommerceApp.Application.Pricing;

/// <summary>
/// Pure calculation - no I/O, no promotion/tax lookups yet (Milestones 7.1/7.2)
/// - so callers pass in the raw base/variant values they already have loaded
/// rather than this service re-querying anything itself.
/// </summary>
public interface IPricingService
{
    PriceResultDto Calculate(decimal basePrice, decimal? baseCompareAtPrice, decimal? variantPrice, decimal? variantCompareAtPrice);
}
