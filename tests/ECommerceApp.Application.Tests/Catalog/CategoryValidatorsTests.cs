using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Catalog.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Catalog;

public class CategoryValidatorsTests
{
    private readonly UpdateCategoryRequestValidator _validator = new();

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
