using ECommerceApp.Application.Taxation.Models;
using ECommerceApp.Application.Taxation.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Taxation;

public class TaxRateValidatorsTests
{
    private readonly CreateTaxRateRequestValidator _createValidator = new();

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
    public void An_empty_country_code_is_rejected()
    {
        var request = ValidRequest() with { CountryCode = "" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_country_code_that_isnt_2_letters_is_rejected()
    {
        var request = ValidRequest() with { CountryCode = "USA" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_empty_tax_category_is_rejected()
    {
        var request = ValidRequest() with { TaxCategory = "" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_negative_rate_is_rejected()
    {
        var request = ValidRequest() with { RatePercent = -1m };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_rate_over_100_is_rejected()
    {
        var request = ValidRequest() with { RatePercent = 100.01m };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_zero_rate_is_valid()
    {
        var request = ValidRequest() with { RatePercent = 0m };

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    private static CreateTaxRateRequest ValidRequest() => new("US", null, "Standard", 8.25m, true);
}
