using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Addresses.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Addresses;

public class AddressValidatorsTests
{
    private readonly CreateAddressRequestValidator _createValidator = new();
    private readonly UpdateAddressRequestValidator _updateValidator = new();

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        _createValidator.Validate(ValidCreateRequest()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_request_with_no_optional_fields_is_valid()
    {
        var request = ValidCreateRequest() with { Label = null, Line2 = null, RegionCode = null };

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_empty_full_name_is_rejected()
    {
        var request = ValidCreateRequest() with { FullName = "" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_empty_phone_is_rejected()
    {
        var request = ValidCreateRequest() with { Phone = "" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_empty_line1_is_rejected()
    {
        var request = ValidCreateRequest() with { Line1 = "" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_empty_city_is_rejected()
    {
        var request = ValidCreateRequest() with { City = "" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_empty_postal_code_is_rejected()
    {
        var request = ValidCreateRequest() with { PostalCode = "" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_empty_country_code_is_rejected()
    {
        var request = ValidCreateRequest() with { CountryCode = "" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_country_code_that_isnt_2_letters_is_rejected()
    {
        var request = ValidCreateRequest() with { CountryCode = "USA" };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void The_update_validator_rejects_a_zero_id()
    {
        var request = ValidUpdateRequest() with { Id = 0 };

        _updateValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_well_formed_update_request_is_valid()
    {
        _updateValidator.Validate(ValidUpdateRequest()).IsValid.Should().BeTrue();
    }

    private static CreateAddressRequest ValidCreateRequest() => new(
        "Home", "Jane Doe", "555-0100", "123 Main St", "Apt 4", "Springfield", "CA", "90210", "US", true);

    private static UpdateAddressRequest ValidUpdateRequest() => new(
        1, "Home", "Jane Doe", "555-0100", "123 Main St", "Apt 4", "Springfield", "CA", "90210", "US", true);
}
