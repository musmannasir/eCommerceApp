using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Application.Shipping.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Shipping;

public class ShippingMethodValidatorsTests
{
    private readonly CreateShippingMethodRequestValidator _createValidator = new();
    private readonly UpdateShippingMethodRequestValidator _updateValidator = new();

    [Fact]
    public void A_well_formed_country_wide_request_is_valid()
    {
        var request = ValidRequest();

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_well_formed_region_scoped_request_is_valid()
    {
        var request = ValidRequest() with { RegionCode = "CA" };

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_empty_name_is_rejected()
    {
        var request = ValidRequest() with { Name = "" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_country_code_that_isnt_2_letters_is_rejected()
    {
        var request = ValidRequest() with { CountryCode = "USA" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_negative_base_rate_is_rejected()
    {
        var request = ValidRequest() with { BaseRate = -1m };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_negative_rate_per_kg_is_rejected()
    {
        var request = ValidRequest() with { RatePerKg = -1m };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_negative_free_shipping_threshold_is_rejected()
    {
        var request = ValidRequest() with { FreeShippingThreshold = -1m };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_max_delivery_days_below_the_minimum_is_rejected()
    {
        var request = ValidRequest() with { EstimatedDeliveryDaysMin = 5, EstimatedDeliveryDaysMax = 2 };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_max_delivery_days_at_or_above_the_minimum_is_valid()
    {
        var request = ValidRequest() with { EstimatedDeliveryDaysMin = 2, EstimatedDeliveryDaysMax = 5 };

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_negative_display_order_is_rejected()
    {
        var request = ValidRequest() with { DisplayOrder = -1 };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_well_formed_update_request_is_valid()
    {
        var request = ValidUpdateRequest();

        _updateValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_zero_id_is_rejected_on_update()
    {
        var request = ValidUpdateRequest() with { Id = 0 };

        _updateValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_negative_base_rate_is_rejected_on_update()
    {
        var request = ValidUpdateRequest() with { BaseRate = -1m };

        _updateValidator.Validate(request).IsValid.Should().BeFalse();
    }

    private static CreateShippingMethodRequest ValidRequest() => new(
        "Standard Shipping", null, "US", null, 5m, 1m, null, null, null, 0, true);

    private static UpdateShippingMethodRequest ValidUpdateRequest() => new(
        1, "Standard Shipping", null, "US", null, 5m, 1m, null, null, null, 0, true);
}
