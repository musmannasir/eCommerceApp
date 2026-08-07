using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Catalog.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Catalog;

public class CategoryValidatorsTests
{
    private readonly CreateCategoryRequestValidator _createValidator = new();
    private readonly UpdateCategoryRequestValidator _validator = new();

    [Fact]
    public void A_well_formed_create_request_is_valid()
    {
        var request = new CreateCategoryRequest("Electronics", null, null, null, 0, true, false);

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_missing_name_is_rejected_on_create()
    {
        var request = new CreateCategoryRequest("", null, null, null, 0, true, false);

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_negative_display_order_is_rejected_on_create()
    {
        var request = new CreateCategoryRequest("Electronics", null, null, null, -1, true, false);

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_well_formed_update_is_valid()
    {
        var request = new UpdateCategoryRequest(1, "Electronics", null, null, null, 0, true, false);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Setting_a_category_as_its_own_parent_is_rejected()
    {
        var request = new UpdateCategoryRequest(1, "Electronics", null, null, ParentCategoryId: 1, 0, true, false);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
