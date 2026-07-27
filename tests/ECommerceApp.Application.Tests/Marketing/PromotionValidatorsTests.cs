using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Application.Marketing.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Marketing;

public class PromotionValidatorsTests
{
    private readonly CreatePromotionRequestValidator _createValidator = new();

    [Fact]
    public void A_well_formed_entire_order_request_is_valid()
    {
        var request = ValidRequest();

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_empty_name_is_rejected()
    {
        var request = ValidRequest() with { Name = "" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_invalid_discount_type_is_rejected()
    {
        var request = ValidRequest() with { DiscountType = "NotAType" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_percentage_discount_over_100_is_rejected()
    {
        var request = ValidRequest() with { DiscountType = "Percentage", DiscountValue = 150m };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_zero_discount_value_is_rejected()
    {
        var request = ValidRequest() with { DiscountValue = 0m };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_invalid_scope_type_is_rejected()
    {
        var request = ValidRequest() with { ScopeType = "NotAScope" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_category_scope_without_a_category_id_is_rejected()
    {
        var request = ValidRequest() with { ScopeType = "Category", ScopeCategoryId = null };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_category_scope_with_a_category_id_is_valid()
    {
        var request = ValidRequest() with { ScopeType = "Category", ScopeCategoryId = 1 };

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_end_date_before_the_start_date_is_rejected()
    {
        var request = ValidRequest() with { StartsAtUtc = DateTime.UtcNow, EndsAtUtc = DateTime.UtcNow.AddDays(-1) };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    private static CreatePromotionRequest ValidRequest() => new(
        "Ten percent off", null, "SAVE10", "Percentage", 10m, "EntireOrder",
        null, null, null, null, null, DateTime.UtcNow, null, null, null, true);
}
