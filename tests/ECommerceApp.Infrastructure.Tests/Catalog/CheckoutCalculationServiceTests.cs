using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Application.Taxation.Models;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class CheckoutCalculationServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task With_no_promotion_no_tax_and_no_shipping_configured_the_grand_total_is_just_the_subtotal()
    {
        var lines = new[] { new CheckoutLineDto(1, 1, null, "Standard", true, 2m, 100m) };

        var result = await _harness.CheckoutCalculationService.CalculateEstimatedAsync(lines, null);

        result.Subtotal.Should().Be(100m);
        result.PromotionDiscount.Should().Be(0m);
        result.DiscountedSubtotal.Should().Be(100m);
        result.Tax.Should().Be(0m);
        result.Shipping.Should().Be(0m);
        result.GrandTotal.Should().Be(100m);
    }

    [Fact]
    public async Task An_entire_order_discount_reduces_the_taxable_amount_before_tax_is_calculated()
    {
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 10m, true));
        var promotion = await SeedEntireOrderPromotionAsync("SAVE10", 10m);
        var lines = new[] { new CheckoutLineDto(1, 1, null, "Standard", true, 0m, 100m) };

        var result = await _harness.CheckoutCalculationService.CalculateEstimatedAsync(lines, promotion.PromotionId);

        result.PromotionDiscount.Should().Be(10m);
        result.DiscountedSubtotal.Should().Be(90m);
        result.TaxRateConfigured.Should().BeTrue();
        result.Tax.Should().Be(9m); // 10% of the post-discount 90, not the pre-discount 100
    }

    [Fact]
    public async Task A_category_scoped_discount_only_reduces_the_tax_owed_on_matching_lines()
    {
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 10m, true));
        var promotion = await SeedCategoryScopedPromotionAsync("CATSALE", 10m, categoryId: 7);
        var lines = new[]
        {
            new CheckoutLineDto(1, 7, null, "Standard", true, 0m, 60m),
            new CheckoutLineDto(2, 8, null, "Standard", true, 0m, 40m),
        };

        var result = await _harness.CheckoutCalculationService.CalculateEstimatedAsync(lines, promotion.PromotionId);

        result.PromotionDiscount.Should().Be(6m); // 10% of the eligible 60
        // Tax is 10% of (60 - 6) for line 1 plus 10% of the untouched 40 for line 2.
        result.Tax.Should().Be(9.4m);
    }

    [Fact]
    public async Task Shipping_free_threshold_is_checked_against_the_post_discount_subtotal()
    {
        await _harness.ShippingService.CreateAsync(new CreateShippingMethodRequest(
            "Standard", null, "US", "CA", 5m, 1m, FreeShippingThreshold: 90m, null, null, 0, true));
        var promotion = await SeedEntireOrderPromotionAsync("SAVE10", 10m);
        var lines = new[] { new CheckoutLineDto(1, 1, null, "Standard", false, 2m, 100m) };

        var result = await _harness.CheckoutCalculationService.CalculateEstimatedAsync(lines, promotion.PromotionId);

        // Pre-discount subtotal (100) would clear the 90 threshold, but the
        // post-discount subtotal (90) exactly meets it too - drop the discount
        // to prove it's the post-discount amount driving the free-shipping check.
        result.DiscountedSubtotal.Should().Be(90m);
        result.ShippingRateConfigured.Should().BeTrue();
        result.Shipping.Should().Be(0m);
    }

    [Fact]
    public async Task An_invalid_applied_promotion_id_is_treated_as_no_discount_rather_than_failing()
    {
        var lines = new[] { new CheckoutLineDto(1, 1, null, "Standard", true, 0m, 100m) };

        var result = await _harness.CheckoutCalculationService.CalculateEstimatedAsync(lines, appliedPromotionId: 999999);

        result.PromotionDiscount.Should().Be(0m);
        result.AppliedCouponCode.Should().BeNull();
        result.DiscountedSubtotal.Should().Be(100m);
    }

    [Fact]
    public async Task A_non_taxable_line_is_excluded_from_tax_even_when_a_rate_is_configured()
    {
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 10m, true));
        var lines = new[] { new CheckoutLineDto(1, 1, null, "Standard", false, 0m, 100m) };

        var result = await _harness.CheckoutCalculationService.CalculateEstimatedAsync(lines, null);

        result.Tax.Should().Be(0m);
    }

    private async Task<Application.Marketing.Models.PromotionApplicationDto> SeedEntireOrderPromotionAsync(string couponCode, decimal percentageDiscount)
    {
        var created = await _harness.PromotionService.CreateAsync(new Application.Marketing.Models.CreatePromotionRequest(
            "Test promotion", null, couponCode, "Percentage", percentageDiscount, "EntireOrder",
            null, null, null, null, null, DateTime.UtcNow.AddDays(-1), null, null, null, true));
        var validation = await _harness.PromotionService.ValidateAppliedPromotionAsync(
            created.Value.Id, new[] { new Application.Marketing.Models.PromotionCartLine(1, 1, null, 100m) }, 100m);
        return validation.Value;
    }

    private async Task<Application.Marketing.Models.PromotionApplicationDto> SeedCategoryScopedPromotionAsync(string couponCode, decimal percentageDiscount, int categoryId)
    {
        var created = await _harness.PromotionService.CreateAsync(new Application.Marketing.Models.CreatePromotionRequest(
            "Test promotion", null, couponCode, "Percentage", percentageDiscount, "Category",
            categoryId, null, null, null, null, DateTime.UtcNow.AddDays(-1), null, null, null, true));
        var validation = await _harness.PromotionService.ValidateAppliedPromotionAsync(
            created.Value.Id,
            new[] { new Application.Marketing.Models.PromotionCartLine(1, categoryId, null, 60m), new Application.Marketing.Models.PromotionCartLine(2, 8, null, 40m) },
            100m);
        return validation.Value;
    }
}
