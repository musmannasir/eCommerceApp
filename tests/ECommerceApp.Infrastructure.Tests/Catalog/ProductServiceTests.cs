using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Domain.Common;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class ProductServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();
    private int _categoryId;

    public ProductServiceTests()
    {
        _categoryId = 0;
    }

    public void Dispose() => _harness.Dispose();

    private async Task<int> CategoryIdAsync()
    {
        if (_categoryId != 0)
        {
            return _categoryId;
        }

        var category = await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("Gadgets", null, null, null, 0, true, false));
        _categoryId = category.Value.Id;
        return _categoryId;
    }

    private async Task<ProductDto> CreateProductAsync(string name = "Widget", string sku = "SKU-1", decimal sellingPrice = 10m)
    {
        var categoryId = await CategoryIdAsync();
        var result = await _harness.ProductService.CreateAsync(new CreateProductRequest(
            name, null, null, null, null, categoryId, sku, 5m, sellingPrice, null, "Standard", true, true, false,
            null, null, null, null, null, null, null, null, null, null));
        return result.Value;
    }

    [Fact]
    public async Task Creating_a_product_succeeds_and_generates_a_slug()
    {
        var product = await CreateProductAsync("Wireless Mouse", "SKU-MOUSE-1");

        product.Slug.Should().Be("wireless-mouse");
        product.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task Creating_a_product_with_a_duplicate_SKU_is_rejected()
    {
        await CreateProductAsync(sku: "DUPLICATE-SKU");

        var categoryId = await CategoryIdAsync();
        var result = await _harness.ProductService.CreateAsync(new CreateProductRequest(
            "Another Widget", null, null, null, null, categoryId, "DUPLICATE-SKU", 5m, 10m, null, "Standard", true, true, false,
            null, null, null, null, null, null, null, null, null, null));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Creating_a_product_for_a_nonexistent_category_is_rejected()
    {
        var result = await _harness.ProductService.CreateAsync(new CreateProductRequest(
            "Orphan", null, null, null, null, 999_999, "SKU-ORPHAN", 5m, 10m, null, "Standard", true, true, false,
            null, null, null, null, null, null, null, null, null, null));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Publishing_requires_the_product_to_be_active()
    {
        var product = await CreateProductAsync();
        await _harness.ProductService.DeactivateAsync(product.Id);

        var result = await _harness.ProductService.PublishAsync(product.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Publishing_an_active_product_sets_the_published_timestamp()
    {
        var product = await CreateProductAsync();

        var result = await _harness.ProductService.PublishAsync(product.Id);

        result.IsSuccess.Should().BeTrue();
        var updated = (await _harness.ProductService.GetByIdAsync(product.Id)).Value;
        updated.IsPublished.Should().BeTrue();
        updated.PublishedAtUtc.Should().Be(_harness.Clock.UtcNow);
    }

    [Fact]
    public async Task Deactivating_a_published_product_automatically_unpublishes_it()
    {
        var product = await CreateProductAsync();
        await _harness.ProductService.PublishAsync(product.Id);

        await _harness.ProductService.DeactivateAsync(product.Id);

        (await _harness.ProductService.GetByIdAsync(product.Id)).Value.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_product_soft_deletes_it_and_it_can_be_restored()
    {
        var product = await CreateProductAsync();

        (await _harness.ProductService.DeleteAsync(product.Id)).IsSuccess.Should().BeTrue();
        (await _harness.ProductService.GetByIdAsync(product.Id)).IsFailure.Should().BeTrue();

        (await _harness.ProductService.RestoreAsync(product.Id)).IsSuccess.Should().BeTrue();
        (await _harness.ProductService.GetByIdAsync(product.Id)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Adding_a_variant_with_a_valid_combination_succeeds()
    {
        var product = await CreateProductAsync();
        var color = (await _harness.AttributeService.CreateAttributeAsync(new CreateProductAttributeRequest("Color"))).Value;
        var red = (await _harness.AttributeService.CreateValueAsync(new CreateProductAttributeValueRequest(color.Id, "Red"))).Value;

        var result = await _harness.ProductService.AddVariantAsync(new CreateVariantRequest(
            product.Id, "SKU-1-RED", null, null, null, null, null, true, [red.Id]));

        result.IsSuccess.Should().BeTrue();
        result.Value.AttributeValues.Should().ContainSingle(v => v.Value == "Red");
    }

    [Fact]
    public async Task Adding_a_variant_with_a_duplicate_SKU_is_rejected()
    {
        var product = await CreateProductAsync();
        var color = (await _harness.AttributeService.CreateAttributeAsync(new CreateProductAttributeRequest("Color"))).Value;
        var red = (await _harness.AttributeService.CreateValueAsync(new CreateProductAttributeValueRequest(color.Id, "Red"))).Value;
        var blue = (await _harness.AttributeService.CreateValueAsync(new CreateProductAttributeValueRequest(color.Id, "Blue"))).Value;

        await _harness.ProductService.AddVariantAsync(new CreateVariantRequest(product.Id, "SAME-SKU", null, null, null, null, null, true, [red.Id]));

        var result = await _harness.ProductService.AddVariantAsync(new CreateVariantRequest(product.Id, "SAME-SKU", null, null, null, null, null, true, [blue.Id]));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Adding_a_variant_with_an_already_used_attribute_combination_is_rejected()
    {
        var product = await CreateProductAsync();
        var color = (await _harness.AttributeService.CreateAttributeAsync(new CreateProductAttributeRequest("Color"))).Value;
        var red = (await _harness.AttributeService.CreateValueAsync(new CreateProductAttributeValueRequest(color.Id, "Red"))).Value;

        await _harness.ProductService.AddVariantAsync(new CreateVariantRequest(product.Id, "SKU-A", null, null, null, null, null, true, [red.Id]));

        // Same product, same exact attribute combination, different SKU - still a duplicate combination.
        var result = await _harness.ProductService.AddVariantAsync(new CreateVariantRequest(product.Id, "SKU-B", null, null, null, null, null, true, [red.Id]));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("variant.duplicate_combination");
    }

    [Fact]
    public async Task Adding_and_removing_a_tag_updates_the_products_tag_list()
    {
        var product = await CreateProductAsync();

        await _harness.ProductService.AddTagAsync(product.Id, "Bestseller");
        var withTag = (await _harness.ProductService.GetByIdAsync(product.Id)).Value;
        withTag.Tags.Should().ContainSingle(t => t.Name == "Bestseller");

        await _harness.ProductService.RemoveTagAsync(product.Id, withTag.Tags[0].Id);
        var withoutTag = (await _harness.ProductService.GetByIdAsync(product.Id)).Value;
        withoutTag.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Adding_the_same_tag_twice_is_rejected()
    {
        var product = await CreateProductAsync();
        await _harness.ProductService.AddTagAsync(product.Id, "Bestseller");

        var result = await _harness.ProductService.AddTagAsync(product.Id, "bestseller");

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Adding_an_image_stores_it_via_the_file_storage_abstraction()
    {
        var product = await CreateProductAsync();
        await using var content = new MemoryStream([1, 2, 3]);

        var result = await _harness.ProductService.AddImageAsync(product.Id, null, content, "photo.jpg", "image/jpeg", "Alt text", true);

        result.IsSuccess.Should().BeTrue();
        _harness.FileStorage.SavedPaths.Should().ContainSingle();
    }

    [Fact]
    public async Task Deleting_an_image_removes_it_from_storage_too()
    {
        var product = await CreateProductAsync();
        await using var content = new MemoryStream([1, 2, 3]);
        var image = (await _harness.ProductService.AddImageAsync(product.Id, null, content, "photo.jpg", "image/jpeg", null, false)).Value;

        var result = await _harness.ProductService.DeleteImageAsync(image.Id);

        result.IsSuccess.Should().BeTrue();
        _harness.FileStorage.DeletedPaths.Should().Contain(image.Path);
    }
}
