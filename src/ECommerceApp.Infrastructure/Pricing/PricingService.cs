using ECommerceApp.Application.Pricing;
using ECommerceApp.Application.Pricing.Models;
using Microsoft.Extensions.Configuration;

namespace ECommerceApp.Infrastructure.Pricing;

public sealed class PricingService : IPricingService
{
    private readonly IConfiguration _configuration;

    public PricingService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public PriceResultDto Calculate(decimal basePrice, decimal? baseCompareAtPrice, decimal? variantPrice, decimal? variantCompareAtPrice)
    {
        var effectivePrice = variantPrice ?? basePrice;
        var effectiveCompareAtPrice = variantCompareAtPrice ?? baseCompareAtPrice;

        // No Promotion entity exists yet (Milestone 7.1) - always 0 until then.
        const decimal promotionAdjustment = 0m;
        var finalPrice = effectivePrice - promotionAdjustment;

        decimal? discountAmount = null;
        int? discountPercent = null;
        if (effectiveCompareAtPrice.HasValue && effectiveCompareAtPrice.Value > finalPrice)
        {
            discountAmount = effectiveCompareAtPrice.Value - finalPrice;
            discountPercent = (int)Math.Round((1 - finalPrice / effectiveCompareAtPrice.Value) * 100);
        }

        var isTaxInclusive = _configuration.GetValue("Store:PricesIncludeTax", false);

        return new PriceResultDto(basePrice, effectivePrice, promotionAdjustment, finalPrice, effectiveCompareAtPrice, discountAmount, discountPercent, isTaxInclusive);
    }
}
