using ECommerceApp.Infrastructure.Pricing;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace ECommerceApp.Infrastructure.Tests.Pricing;

public class PricingServiceTests
{
    private readonly PricingService _service = new(new ConfigurationBuilder().Build());

    [Fact]
    public void With_no_variant_override_the_base_price_is_used()
    {
        var result = _service.Calculate(basePrice: 100m, baseCompareAtPrice: null, variantPrice: null, variantCompareAtPrice: null);

        result.VariantPrice.Should().Be(100m);
        result.FinalPrice.Should().Be(100m);
    }

    [Fact]
    public void A_variant_price_override_replaces_the_base_price()
    {
        var result = _service.Calculate(basePrice: 100m, baseCompareAtPrice: null, variantPrice: 120m, variantCompareAtPrice: null);

        result.VariantPrice.Should().Be(120m);
        result.FinalPrice.Should().Be(120m);
    }

    [Fact]
    public void A_variant_compare_at_override_replaces_the_base_compare_at()
    {
        var result = _service.Calculate(basePrice: 100m, baseCompareAtPrice: 110m, variantPrice: null, variantCompareAtPrice: 150m);

        result.CompareAtPrice.Should().Be(150m);
    }

    [Fact]
    public void Without_a_variant_compare_at_override_the_base_compare_at_is_used()
    {
        var result = _service.Calculate(basePrice: 100m, baseCompareAtPrice: 110m, variantPrice: 90m, variantCompareAtPrice: null);

        result.CompareAtPrice.Should().Be(110m);
    }

    [Fact]
    public void A_compare_at_price_above_the_final_price_produces_a_discount()
    {
        var result = _service.Calculate(basePrice: 80m, baseCompareAtPrice: 100m, variantPrice: null, variantCompareAtPrice: null);

        result.DiscountAmount.Should().Be(20m);
        result.DiscountPercent.Should().Be(20);
    }

    [Fact]
    public void No_discount_is_reported_when_there_is_no_compare_at_price()
    {
        var result = _service.Calculate(basePrice: 80m, baseCompareAtPrice: null, variantPrice: null, variantCompareAtPrice: null);

        result.DiscountAmount.Should().BeNull();
        result.DiscountPercent.Should().BeNull();
    }

    [Fact]
    public void No_discount_is_reported_when_the_compare_at_price_is_not_actually_higher()
    {
        var result = _service.Calculate(basePrice: 80m, baseCompareAtPrice: 80m, variantPrice: null, variantCompareAtPrice: null);

        result.DiscountAmount.Should().BeNull();
        result.DiscountPercent.Should().BeNull();
    }

    [Fact]
    public void There_is_no_promotion_data_yet_so_the_adjustment_is_always_zero()
    {
        var result = _service.Calculate(basePrice: 100m, baseCompareAtPrice: null, variantPrice: null, variantCompareAtPrice: null);

        result.PromotionAdjustment.Should().Be(0m);
        result.FinalPrice.Should().Be(result.VariantPrice);
    }

    [Fact]
    public void Tax_inclusive_defaults_to_false_when_not_configured()
    {
        var result = _service.Calculate(basePrice: 100m, baseCompareAtPrice: null, variantPrice: null, variantCompareAtPrice: null);

        result.IsTaxInclusive.Should().BeFalse();
    }

    [Fact]
    public void Tax_inclusive_reflects_the_configured_store_setting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Store:PricesIncludeTax"] = "true" })
            .Build();
        var service = new PricingService(configuration);

        var result = service.Calculate(basePrice: 100m, baseCompareAtPrice: null, variantPrice: null, variantCompareAtPrice: null);

        result.IsTaxInclusive.Should().BeTrue();
    }
}
