using ECommerceApp.Application.Configuration;
using ECommerceApp.Application.Pricing;
using ECommerceApp.Application.Pricing.Models;

namespace ECommerceApp.Infrastructure.Pricing;

public sealed class PricingService : IPricingService
{
    private readonly IStoreSettingsService _storeSettingsService;

    public PricingService(IStoreSettingsService storeSettingsService)
    {
        _storeSettingsService = storeSettingsService;
    }

    public async Task<PriceResultDto> CalculateAsync(decimal basePrice, decimal? baseCompareAtPrice, decimal? variantPrice, decimal? variantCompareAtPrice, CancellationToken cancellationToken = default)
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

        var storeSettings = await _storeSettingsService.GetAsync(cancellationToken);
        var isTaxInclusive = storeSettings.PricesIncludeTax;

        return new PriceResultDto(basePrice, effectivePrice, promotionAdjustment, finalPrice, effectiveCompareAtPrice, discountAmount, discountPercent, isTaxInclusive);
    }
}
