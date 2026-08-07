using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Catalog.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Catalog;

public class BrandValidatorsTests
{
    private readonly CreateBrandRequestValidator _createValidator = new();
    private readonly UpdateBrandRequestValidator _updateValidator = new();

    [Fact]
    public void A_well_formed_create_request_is_valid()
    {
        var request = new CreateBrandRequest("Acme", null, null, null, true, false);

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_missing_name_is_rejected_on_create()
    {
        var request = new CreateBrandRequest("", null, null, null, true, false);

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_name_over_the_maximum_length_is_rejected_on_create()
    {
        var request = new CreateBrandRequest(new string('a', 201), null, null, null, true, false);

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_well_formed_update_request_is_valid()
    {
        var request = new UpdateBrandRequest(1, "Acme", null, null, null, true, false);

        _updateValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_zero_id_is_rejected_on_update()
    {
        var request = new UpdateBrandRequest(0, "Acme", null, null, null, true, false);

        _updateValidator.Validate(request).IsValid.Should().BeFalse();
    }
}
