using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Domain.Common;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class PromotionServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Creating_a_promotion_succeeds_with_valid_data()
    {
        var result = await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 10m));

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Ten percent off");
        result.Value.CouponCode.Should().Be("SAVE10");
    }

    [Fact]
    public async Task Creating_a_promotion_with_a_coupon_code_already_in_use_is_rejected()
    {
        await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 10m));

        var result = await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 20m));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Updating_a_promotion_persists_changes()
    {
        var created = await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 10m));

        var result = await _harness.PromotionService.UpdateAsync(new UpdatePromotionRequest(
            created.Value.Id, "Updated name", null, "SAVE10", "Percentage", 15m, "EntireOrder",
            null, null, null, null, null, DateTime.UtcNow.AddDays(-1), null, null, null, true));

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Updated name");
        result.Value.DiscountValue.Should().Be(15m);
    }

    [Fact]
    public async Task Deleting_a_promotion_soft_deletes_it_and_it_no_longer_appears_in_the_paged_list()
    {
        var created = await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 10m));

        await _harness.PromotionService.DeleteAsync(created.Value.Id);

        var page = await _harness.PromotionService.GetPagedAsync(new PagedQuery());
        page.Value.Items.Should().NotContain(p => p.Id == created.Value.Id);

        var deletedPage = await _harness.PromotionService.GetPagedAsync(new PagedQuery { OnlyDeleted = true });
        deletedPage.Value.Items.Should().Contain(p => p.Id == created.Value.Id);
    }

    [Fact]
    public async Task Restoring_a_deleted_promotion_makes_it_visible_again()
    {
        var created = await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 10m));
        await _harness.PromotionService.DeleteAsync(created.Value.Id);

        await _harness.PromotionService.RestoreAsync(created.Value.Id);

        var page = await _harness.PromotionService.GetPagedAsync(new PagedQuery());
        page.Value.Items.Should().Contain(p => p.Id == created.Value.Id);
    }

    [Fact]
    public async Task Deactivating_and_reactivating_a_promotion_updates_its_active_flag()
    {
        var created = await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 10m));

        await _harness.PromotionService.DeactivateAsync(created.Value.Id);
        (await _harness.PromotionService.GetByIdAsync(created.Value.Id)).Value.IsActive.Should().BeFalse();

        await _harness.PromotionService.ActivateAsync(created.Value.Id);
        (await _harness.PromotionService.GetByIdAsync(created.Value.Id)).Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_valid_percentage_code_discounts_the_subtotal()
    {
        await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 10m));
        var lines = new[] { new PromotionCartLine(1, 1, null, 100m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("SAVE10", lines, 100m);

        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountAmount.Should().Be(10m);
    }

    [Fact]
    public async Task A_valid_fixed_amount_code_discounts_by_the_fixed_value()
    {
        await _harness.PromotionService.CreateAsync(EntireOrderRequest("FLAT5", 5m, discountType: "FixedAmount"));
        var lines = new[] { new PromotionCartLine(1, 1, null, 100m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("FLAT5", lines, 100m);

        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountAmount.Should().Be(5m);
    }

    [Fact]
    public async Task A_fixed_amount_discount_never_exceeds_the_eligible_amount()
    {
        await _harness.PromotionService.CreateAsync(EntireOrderRequest("FLAT50", 50m, discountType: "FixedAmount"));
        var lines = new[] { new PromotionCartLine(1, 1, null, 20m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("FLAT50", lines, 20m);

        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountAmount.Should().Be(20m);
    }

    [Fact]
    public async Task MaxDiscountAmount_caps_a_percentage_discount()
    {
        var request = EntireOrderRequest("SAVE10", 10m) with { MaxDiscountAmount = 5m };
        await _harness.PromotionService.CreateAsync(request);
        var lines = new[] { new PromotionCartLine(1, 1, null, 100m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("SAVE10", lines, 100m);

        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountAmount.Should().Be(5m);
    }

    [Fact]
    public async Task An_expired_code_is_rejected()
    {
        var request = EntireOrderRequest("SAVE10", 10m) with
        {
            StartsAtUtc = DateTime.UtcNow.AddDays(-10),
            EndsAtUtc = DateTime.UtcNow.AddDays(-1),
        };
        await _harness.PromotionService.CreateAsync(request);
        var lines = new[] { new PromotionCartLine(1, 1, null, 100m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("SAVE10", lines, 100m);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task A_not_yet_started_code_is_rejected()
    {
        var request = EntireOrderRequest("SAVE10", 10m) with { StartsAtUtc = DateTime.UtcNow.AddDays(5) };
        await _harness.PromotionService.CreateAsync(request);
        var lines = new[] { new PromotionCartLine(1, 1, null, 100m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("SAVE10", lines, 100m);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task An_inactive_code_is_rejected()
    {
        var request = EntireOrderRequest("SAVE10", 10m) with { IsActive = false };
        await _harness.PromotionService.CreateAsync(request);
        var lines = new[] { new PromotionCartLine(1, 1, null, 100m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("SAVE10", lines, 100m);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task A_code_below_the_minimum_order_amount_is_rejected()
    {
        var request = EntireOrderRequest("SAVE10", 10m) with { MinimumOrderAmount = 50m };
        await _harness.PromotionService.CreateAsync(request);
        var lines = new[] { new PromotionCartLine(1, 1, null, 30m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("SAVE10", lines, 30m);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task A_category_scoped_code_only_discounts_matching_lines()
    {
        var request = EntireOrderRequest("CATSALE", 10m) with { ScopeType = "Category", ScopeCategoryId = 7 };
        await _harness.PromotionService.CreateAsync(request);
        var lines = new[]
        {
            new PromotionCartLine(1, 7, null, 60m),
            new PromotionCartLine(2, 8, null, 40m),
        };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("CATSALE", lines, 100m);

        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountAmount.Should().Be(6m);
    }

    [Fact]
    public async Task A_category_scoped_code_with_no_matching_lines_is_rejected()
    {
        var request = EntireOrderRequest("CATSALE", 10m) with { ScopeType = "Category", ScopeCategoryId = 7 };
        await _harness.PromotionService.CreateAsync(request);
        var lines = new[] { new PromotionCartLine(2, 8, null, 40m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("CATSALE", lines, 40m);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Coupon_code_lookup_is_case_insensitive()
    {
        await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 10m));
        var lines = new[] { new PromotionCartLine(1, 1, null, 100m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("save10", lines, 100m);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task An_unknown_code_is_not_found()
    {
        var lines = new[] { new PromotionCartLine(1, 1, null, 100m) };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("NOPE", lines, 100m);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task LineDiscounts_for_an_entire_order_promotion_are_split_proportionally_across_all_lines()
    {
        await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 10m));
        var lines = new[]
        {
            new PromotionCartLine(1, 1, null, 75m),
            new PromotionCartLine(2, 1, null, 25m),
        };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("SAVE10", lines, 100m);

        result.IsSuccess.Should().BeTrue();
        result.Value.LineDiscounts.Should().HaveCount(2);
        result.Value.LineDiscounts[0].Should().Be(7.5m);
        result.Value.LineDiscounts[1].Should().Be(2.5m);
        result.Value.LineDiscounts.Sum().Should().Be(result.Value.DiscountAmount);
    }

    [Fact]
    public async Task LineDiscounts_for_a_category_scoped_promotion_are_zero_for_lines_outside_the_scope()
    {
        var request = EntireOrderRequest("CATSALE", 10m) with { ScopeType = "Category", ScopeCategoryId = 7 };
        await _harness.PromotionService.CreateAsync(request);
        var lines = new[]
        {
            new PromotionCartLine(1, 7, null, 60m),
            new PromotionCartLine(2, 8, null, 40m),
        };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("CATSALE", lines, 100m);

        result.IsSuccess.Should().BeTrue();
        result.Value.LineDiscounts[0].Should().Be(6m);
        result.Value.LineDiscounts[1].Should().Be(0m);
    }

    [Fact]
    public async Task LineDiscounts_always_sum_to_exactly_the_discount_amount_despite_rounding()
    {
        await _harness.PromotionService.CreateAsync(EntireOrderRequest("SAVE10", 10m));
        var lines = new[]
        {
            new PromotionCartLine(1, 1, null, 10m),
            new PromotionCartLine(2, 1, null, 10m),
            new PromotionCartLine(3, 1, null, 10m),
        };

        var result = await _harness.PromotionService.FindApplicablePromotionAsync("SAVE10", lines, 30m);

        result.IsSuccess.Should().BeTrue();
        result.Value.LineDiscounts.Sum().Should().Be(result.Value.DiscountAmount);
    }

    private static CreatePromotionRequest EntireOrderRequest(string couponCode, decimal discountValue, string discountType = "Percentage") =>
        new(
            "Ten percent off", null, couponCode, discountType, discountValue, "EntireOrder",
            null, null, null, null, null, DateTime.UtcNow.AddDays(-1), null, null, null, true);
}
