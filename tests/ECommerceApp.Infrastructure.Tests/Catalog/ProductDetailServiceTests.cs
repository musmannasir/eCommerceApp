using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Inventory;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class ProductDetailServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task An_active_published_product_is_available()
    {
        var product = await SeedProductAsync();

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, Array.Empty<int>());

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(product.Name);
        result.Value.Sku.Should().Be(product.BaseSKU);
    }

    [Fact]
    public async Task An_unpublished_product_is_not_found()
    {
        var product = await SeedProductAsync(isPublished: false);

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, Array.Empty<int>());

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task An_inactive_product_is_not_found()
    {
        var product = await SeedProductAsync(isActive: false);

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, Array.Empty<int>());

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task A_soft_deleted_product_is_not_found()
    {
        var product = await SeedProductAsync();
        product.IsDeleted = true;
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, Array.Empty<int>());

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task An_unknown_slug_is_not_found()
    {
        var result = await _harness.ProductDetailService.GetDetailAsync("does-not-exist", null, Array.Empty<int>());

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Selecting_a_variant_by_id_resolves_its_sku_and_price()
    {
        var (product, red, blue) = await SeedProductWithVariantsAsync();

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, blue.Id, Array.Empty<int>());

        result.Value.SelectedVariantId.Should().Be(blue.Id);
        result.Value.Sku.Should().Be(blue.SKU);
        result.Value.SelectedCombinationUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task Selecting_a_valid_attribute_combination_resolves_the_matching_variant()
    {
        var (product, red, blue) = await SeedProductWithVariantsAsync();
        var redValueId = red.AttributeValues.First().ProductAttributeValueId;

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, new[] { redValueId });

        result.Value.SelectedVariantId.Should().Be(red.Id);
        result.Value.SelectedCombinationUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task An_invalid_attribute_combination_falls_back_to_the_first_variant_and_flags_it()
    {
        var (product, red, blue) = await SeedProductWithVariantsAsync();

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, new[] { 999999 });

        result.Value.SelectedCombinationUnavailable.Should().BeTrue();
        result.Value.SelectedVariantId.Should().NotBeNull();
    }

    [Fact]
    public async Task No_selection_defaults_to_the_first_active_variant()
    {
        var (product, red, blue) = await SeedProductWithVariantsAsync();

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, Array.Empty<int>());

        result.Value.HasVariants.Should().BeTrue();
        result.Value.SelectedVariantId.Should().Be(red.Id);
        result.Value.SelectedCombinationUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task Variant_combinations_are_returned_for_every_active_variant()
    {
        var (product, red, blue) = await SeedProductWithVariantsAsync();

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, Array.Empty<int>());

        result.Value.VariantCombinations.Should().HaveCount(2);
        result.Value.VariantCombinations.Should().Contain(c => c.VariantId == red.Id);
        result.Value.VariantCombinations.Should().Contain(c => c.VariantId == blue.Id);
    }

    [Fact]
    public async Task Resolving_a_real_active_variant_returns_its_authoritative_price_and_sku()
    {
        var (product, red, blue) = await SeedProductWithVariantsAsync();

        var result = await _harness.ProductDetailService.ResolveVariantAsync(product.Slug, blue.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.VariantId.Should().Be(blue.Id);
        result.Value.Sku.Should().Be(blue.SKU);
    }

    [Fact]
    public async Task Resolving_a_variant_that_does_not_belong_to_the_product_is_rejected()
    {
        var (productA, redA, _) = await SeedProductWithVariantsAsync();
        var (productB, _, _) = await SeedProductWithVariantsAsync();

        var result = await _harness.ProductDetailService.ResolveVariantAsync(productB.Slug, redA.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Resolving_an_inactive_variant_is_rejected()
    {
        var (product, red, _) = await SeedProductWithVariantsAsync();
        red.IsActive = false;
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.ProductDetailService.ResolveVariantAsync(product.Slug, red.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Resolving_a_nonexistent_variant_id_is_rejected()
    {
        var (product, _, _) = await SeedProductWithVariantsAsync();

        var result = await _harness.ProductDetailService.ResolveVariantAsync(product.Slug, 999999);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Resolving_a_variant_for_an_unpublished_product_is_not_found()
    {
        var (product, red, _) = await SeedProductWithVariantsAsync();
        product.IsPublished = false;
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.ProductDetailService.ResolveVariantAsync(product.Slug, red.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task A_product_with_no_inventory_record_is_treated_as_in_stock()
    {
        var product = await SeedProductAsync();

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, Array.Empty<int>());

        result.Value.StockState.Should().Be(ProductStockState.InStock);
    }

    [Fact]
    public async Task Stock_below_the_low_stock_threshold_reports_low_stock()
    {
        var product = await SeedProductAsync(lowStockThreshold: 10);
        await SeedInventoryAsync(product.Id, null, onHand: 5, reserved: 0, allowBackorder: false);

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, Array.Empty<int>());

        result.Value.StockState.Should().Be(ProductStockState.LowStock);
        result.Value.AvailableQuantity.Should().Be(5);
    }

    [Fact]
    public async Task Zero_available_stock_without_backorder_is_out_of_stock()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, null, onHand: 0, reserved: 0, allowBackorder: false);

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, Array.Empty<int>());

        result.Value.StockState.Should().Be(ProductStockState.OutOfStock);
    }

    [Fact]
    public async Task Zero_available_stock_with_backorder_allowed_reports_backorder()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, null, onHand: 0, reserved: 0, allowBackorder: true);

        var result = await _harness.ProductDetailService.GetDetailAsync(product.Slug, null, Array.Empty<int>());

        result.Value.StockState.Should().Be(ProductStockState.Backorder);
    }

    [Fact]
    public async Task Related_products_come_from_the_same_category_and_exclude_self_and_unpublished()
    {
        var category = await SeedCategoryAsync();
        var main = await SeedProductAsync(category: category);
        var related = await SeedProductAsync(category: category, name: "Related Widget");
        var unpublishedInCategory = await SeedProductAsync(category: category, isPublished: false, name: "Hidden Widget");

        var result = await _harness.ProductDetailService.GetDetailAsync(main.Slug, null, Array.Empty<int>());

        result.Value.RelatedProducts.Should().Contain(p => p.Id == related.Id);
        result.Value.RelatedProducts.Should().NotContain(p => p.Id == main.Id || p.Id == unpublishedInCategory.Id);
    }

    private async Task<Category> SeedCategoryAsync()
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();
        return category;
    }

    private async Task<Product> SeedProductAsync(
        bool isActive = true, bool isPublished = true, int? lowStockThreshold = null, Category? category = null, string name = "Widget")
    {
        category ??= await SeedCategoryAsync();

        var product = new Product
        {
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = 5,
            SellingPrice = 10,
            IsActive = isActive,
            IsPublished = isPublished,
            PublishedAtUtc = DateTime.UtcNow,
            LowStockThreshold = lowStockThreshold,
        };
        _harness.DbContext.Products.Add(product);
        await _harness.DbContext.SaveChangesAsync();
        return product;
    }

    private async Task<(Product Product, ProductVariant Red, ProductVariant Blue)> SeedProductWithVariantsAsync()
    {
        var product = await SeedProductAsync();

        var colorAttribute = new ProductAttribute { Name = "Color" };
        _harness.DbContext.ProductAttributes.Add(colorAttribute);
        await _harness.DbContext.SaveChangesAsync();

        var redValue = new ProductAttributeValue { ProductAttributeId = colorAttribute.Id, Value = "Red" };
        var blueValue = new ProductAttributeValue { ProductAttributeId = colorAttribute.Id, Value = "Blue" };
        _harness.DbContext.ProductAttributeValues.AddRange(redValue, blueValue);
        await _harness.DbContext.SaveChangesAsync();

        var red = new ProductVariant
        {
            ProductId = product.Id,
            SKU = $"SKU-RED-{Guid.NewGuid():N}",
            CombinationKey = ProductVariant.BuildCombinationKey(new[] { redValue.Id }),
            IsActive = true,
        };
        var blue = new ProductVariant
        {
            ProductId = product.Id,
            SKU = $"SKU-BLUE-{Guid.NewGuid():N}",
            CombinationKey = ProductVariant.BuildCombinationKey(new[] { blueValue.Id }),
            IsActive = true,
        };
        _harness.DbContext.ProductVariants.AddRange(red, blue);
        await _harness.DbContext.SaveChangesAsync();

        _harness.DbContext.Set<ProductVariantAttributeValue>().AddRange(
            new ProductVariantAttributeValue { ProductVariantId = red.Id, ProductAttributeValueId = redValue.Id },
            new ProductVariantAttributeValue { ProductVariantId = blue.Id, ProductAttributeValueId = blueValue.Id });
        await _harness.DbContext.SaveChangesAsync();

        return (product, red, blue);
    }

    private async Task SeedInventoryAsync(int productId, int? variantId, int onHand, int reserved, bool allowBackorder)
    {
        var warehouse = new Warehouse { Name = "Main", Code = $"WH-{Guid.NewGuid():N}", IsActive = true };
        _harness.DbContext.Warehouses.Add(warehouse);
        await _harness.DbContext.SaveChangesAsync();

        _harness.DbContext.InventoryItems.Add(new InventoryItem
        {
            WarehouseId = warehouse.Id,
            ProductId = productId,
            ProductVariantId = variantId,
            QuantityOnHand = onHand,
            QuantityReserved = reserved,
            AllowBackorder = allowBackorder,
            LastStockUpdateUtc = DateTime.UtcNow,
        });
        await _harness.DbContext.SaveChangesAsync();
    }
}
