using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Carts.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Carts;

public class CartValidatorsTests
{
    private readonly AddCartItemRequestValidator _addValidator = new();
    private readonly UpdateCartItemQuantityRequestValidator _updateValidator = new();

    [Fact]
    public void A_well_formed_add_request_is_valid()
    {
        var request = new AddCartItemRequest(1, null, 2);

        _addValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_zero_product_id_is_rejected_on_add()
    {
        var request = new AddCartItemRequest(0, null, 2);

        _addValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_zero_product_variant_id_is_rejected_when_provided_on_add()
    {
        var request = new AddCartItemRequest(1, 0, 2);

        _addValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_zero_quantity_is_rejected_on_add()
    {
        var request = new AddCartItemRequest(1, null, 0);

        _addValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_well_formed_update_quantity_request_is_valid()
    {
        var request = new UpdateCartItemQuantityRequest(1, 3);

        _updateValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_zero_quantity_is_rejected_on_update()
    {
        var request = new UpdateCartItemQuantityRequest(1, 0);

        _updateValidator.Validate(request).IsValid.Should().BeFalse();
    }
}
