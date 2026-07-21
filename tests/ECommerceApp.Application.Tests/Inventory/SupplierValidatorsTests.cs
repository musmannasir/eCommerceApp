using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Application.Inventory.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Inventory;

public class SupplierValidatorsTests
{
    private readonly CreateSupplierRequestValidator _createValidator = new();
    private readonly LinkSupplierProductRequestValidator _linkValidator = new();

    [Fact]
    public void A_well_formed_create_request_is_valid()
    {
        var request = new CreateSupplierRequest("Acme", "ACME", null, "jane@acme.test", null, null, null, null, null, null, null, null, null, true);

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_empty_name_is_rejected()
    {
        var request = new CreateSupplierRequest("", "ACME", null, null, null, null, null, null, null, null, null, null, null, true);

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_empty_code_is_rejected()
    {
        var request = new CreateSupplierRequest("Acme", "", null, null, null, null, null, null, null, null, null, null, null, true);

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_invalid_email_is_rejected()
    {
        var request = new CreateSupplierRequest("Acme", "ACME", null, "not-an-email", null, null, null, null, null, null, null, null, null, true);

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_negative_cost_price_on_a_product_link_is_rejected()
    {
        var request = new LinkSupplierProductRequest(1, 1, null, -1m, null, false);

        _linkValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_negative_lead_time_on_a_product_link_is_rejected()
    {
        var request = new LinkSupplierProductRequest(1, 1, null, null, -1, false);

        _linkValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_well_formed_link_request_is_valid()
    {
        var request = new LinkSupplierProductRequest(1, 1, "SUP-1", 4.5m, 3, true);

        _linkValidator.Validate(request).IsValid.Should().BeTrue();
    }
}
