using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Catalog.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Catalog;

public class ProductValidatorsTests
{
    private readonly CreateProductRequestValidator _validator = new();

    private static CreateProductRequest ValidRequest(decimal sellingPrice = 10m, decimal? compareAtPrice = null) => new(
        "Widget", null, null, null, null, 1, "SKU-1", 5m, sellingPrice, compareAtPrice, "Standard", true, true, false,
        null, null, null, null, null, null, null, null, null, null);

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        _validator.Validate(ValidRequest()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_negative_selling_price_is_rejected()
    {
        var request = ValidRequest(sellingPrice: -5m);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_zero_selling_price_is_rejected()
    {
        var request = ValidRequest(sellingPrice: 0m);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_compare_at_price_lower_than_the_selling_price_is_rejected()
    {
        var request = ValidRequest(sellingPrice: 10m, compareAtPrice: 5m);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_compare_at_price_higher_than_the_selling_price_is_valid()
    {
        var request = ValidRequest(sellingPrice: 10m, compareAtPrice: 15m);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_missing_category_is_rejected()
    {
        var request = ValidRequest() with { CategoryId = 0 };

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_missing_SKU_is_rejected()
    {
        var request = ValidRequest() with { BaseSKU = "" };

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}

public class UpdateProductRequestValidatorTests
{
    private readonly UpdateProductRequestValidator _validator = new();

    private static UpdateProductRequest ValidRequest(int id = 1, decimal sellingPrice = 10m, decimal? compareAtPrice = null) => new(
        id, "Widget", null, null, null, null, 1, "SKU-1", 5m, sellingPrice, compareAtPrice, "Standard", true, true, false,
        null, null, null, null, null, null, null, null, null, null);

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        _validator.Validate(ValidRequest()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_zero_id_is_rejected()
    {
        var request = ValidRequest(id: 0);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_compare_at_price_lower_than_the_selling_price_is_rejected()
    {
        var request = ValidRequest(sellingPrice: 10m, compareAtPrice: 5m);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}

public class CreateVariantRequestValidatorTests
{
    private readonly CreateVariantRequestValidator _validator = new();

    private static CreateVariantRequest ValidRequest() => new(1, "SKU-VAR-1", null, null, null, null, null, true, [1]);

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        _validator.Validate(ValidRequest()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_missing_attribute_value_selection_is_rejected()
    {
        var request = ValidRequest() with { AttributeValueIds = [] };

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_compare_at_price_lower_than_the_selling_price_is_rejected()
    {
        var request = ValidRequest() with { SellingPrice = 10m, CompareAtPrice = 5m };

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}

public class CreateSpecificationRequestValidatorTests
{
    private readonly CreateSpecificationRequestValidator _validator = new();

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        var request = new CreateSpecificationRequest(1, "Color", "Black", 0);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, "Color", "Black")]
    [InlineData(1, "", "Black")]
    [InlineData(1, "Color", "")]
    public void Missing_or_malformed_fields_are_rejected(int productId, string name, string value)
    {
        var request = new CreateSpecificationRequest(productId, name, value, 0);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
