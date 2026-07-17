using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Domain.Common;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class BrandServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Creating_a_brand_succeeds_and_generates_a_slug()
    {
        var result = await _harness.BrandService.CreateAsync(new CreateBrandRequest("Acme Corp", null, null, null, true, false));

        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be("acme-corp");
    }

    [Fact]
    public async Task Creating_a_brand_with_a_duplicate_slug_is_rejected()
    {
        await _harness.BrandService.CreateAsync(new CreateBrandRequest("Acme Corp", null, null, null, true, false));

        var result = await _harness.BrandService.CreateAsync(new CreateBrandRequest("ACME CORP", null, null, null, true, false));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Deactivating_and_reactivating_a_brand_toggles_its_status()
    {
        var brand = (await _harness.BrandService.CreateAsync(new CreateBrandRequest("Acme", null, null, null, true, false))).Value;

        await _harness.BrandService.DeactivateAsync(brand.Id);
        (await _harness.BrandService.GetByIdAsync(brand.Id)).Value.IsActive.Should().BeFalse();

        await _harness.BrandService.ActivateAsync(brand.Id);
        (await _harness.BrandService.GetByIdAsync(brand.Id)).Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_brand_referenced_by_a_product_is_rejected()
    {
        var brand = (await _harness.BrandService.CreateAsync(new CreateBrandRequest("Acme", null, null, null, true, false))).Value;
        var category = (await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("Gadgets", null, null, null, 0, true, false))).Value;

        await _harness.ProductService.CreateAsync(new CreateProductRequest(
            "Widget", null, null, null, brand.Id, category.Id, "SKU-1", 5m, 10m, null, "Standard", true, true, false,
            null, null, null, null, null, null, null, null, null, null));

        var result = await _harness.BrandService.DeleteAsync(brand.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }
}
