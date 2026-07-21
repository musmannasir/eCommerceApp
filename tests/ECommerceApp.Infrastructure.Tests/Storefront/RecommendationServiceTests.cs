using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Tests.Catalog;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Storefront;

public class RecommendationServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A_product_in_the_same_category_is_recommended()
    {
        var category = await SeedCategoryAsync();
        var source = await SeedProductAsync(category, price: 10m);
        var sameCategory = await SeedProductAsync(category, price: 10m);
        var otherCategoryProduct = await SeedProductAsync(await SeedCategoryAsync(), price: 1000m);

        var result = await _harness.RecommendationService.GetRecommendationsAsync(source.Id, 10);

        result.Should().Contain(p => p.Id == sameCategory.Id);
        result.Should().NotContain(p => p.Id == otherCategoryProduct.Id);
    }

    [Fact]
    public async Task A_product_with_the_same_brand_but_different_category_is_still_recommended()
    {
        var brand = await SeedBrandAsync();
        var source = await SeedProductAsync(await SeedCategoryAsync(), brand: brand);
        var sameBrand = await SeedProductAsync(await SeedCategoryAsync(), brand: brand);

        var result = await _harness.RecommendationService.GetRecommendationsAsync(source.Id, 10);

        result.Should().Contain(p => p.Id == sameBrand.Id);
    }

    [Fact]
    public async Task A_product_sharing_a_tag_is_recommended()
    {
        var tag = new ProductTag { Name = "Wireless", Slug = $"wireless-{Guid.NewGuid():N}" };
        _harness.DbContext.ProductTags.Add(tag);
        await _harness.DbContext.SaveChangesAsync();

        var source = await SeedProductAsync(await SeedCategoryAsync());
        var sharedTag = await SeedProductAsync(await SeedCategoryAsync());
        await TagProductAsync(source.Id, tag.Id);
        await TagProductAsync(sharedTag.Id, tag.Id);

        var result = await _harness.RecommendationService.GetRecommendationsAsync(source.Id, 10);

        result.Should().Contain(p => p.Id == sharedTag.Id);
    }

    [Fact]
    public async Task A_product_within_the_price_tolerance_but_in_a_different_category_is_still_recommended()
    {
        var source = await SeedProductAsync(await SeedCategoryAsync(), price: 100m);
        var withinTolerance = await SeedProductAsync(await SeedCategoryAsync(), price: 120m);
        var outsideTolerance = await SeedProductAsync(await SeedCategoryAsync(), price: 500m);

        var result = await _harness.RecommendationService.GetRecommendationsAsync(source.Id, 10);

        result.Should().Contain(p => p.Id == withinTolerance.Id);
        result.Should().NotContain(p => p.Id == outsideTolerance.Id);
    }

    [Fact]
    public async Task Products_with_no_shared_signal_are_not_recommended()
    {
        var source = await SeedProductAsync(await SeedCategoryAsync(), price: 100m);
        var unrelated = await SeedProductAsync(await SeedCategoryAsync(), price: 900m);

        var result = await _harness.RecommendationService.GetRecommendationsAsync(source.Id, 10);

        result.Should().NotContain(p => p.Id == unrelated.Id);
    }

    [Fact]
    public async Task Higher_scoring_products_are_ordered_before_lower_scoring_ones()
    {
        var category = await SeedCategoryAsync();
        var brand = await SeedBrandAsync();
        var source = await SeedProductAsync(category, brand: brand, price: 100m);
        var categoryAndBrandMatch = await SeedProductAsync(category, brand: brand, price: 100m, name: "Best match");
        var categoryOnlyMatch = await SeedProductAsync(category, price: 900m, name: "Weaker match");

        var result = (await _harness.RecommendationService.GetRecommendationsAsync(source.Id, 10)).ToList();

        result.IndexOf(result.First(p => p.Id == categoryAndBrandMatch.Id))
            .Should().BeLessThan(result.IndexOf(result.First(p => p.Id == categoryOnlyMatch.Id)));
    }

    [Fact]
    public async Task Unpublished_and_inactive_candidates_are_excluded()
    {
        var category = await SeedCategoryAsync();
        var source = await SeedProductAsync(category);
        var unpublished = await SeedProductAsync(category, isPublished: false);
        var inactive = await SeedProductAsync(category, isActive: false);

        var result = await _harness.RecommendationService.GetRecommendationsAsync(source.Id, 10);

        result.Should().NotContain(p => p.Id == unpublished.Id || p.Id == inactive.Id);
    }

    [Fact]
    public async Task The_source_product_itself_is_never_recommended()
    {
        var source = await SeedProductAsync(await SeedCategoryAsync());

        var result = await _harness.RecommendationService.GetRecommendationsAsync(source.Id, 10);

        result.Should().NotContain(p => p.Id == source.Id);
    }

    [Fact]
    public async Task An_unknown_source_product_returns_an_empty_list()
    {
        var result = await _harness.RecommendationService.GetRecommendationsAsync(999999, 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task No_qualifying_candidates_returns_an_empty_list()
    {
        var source = await SeedProductAsync(await SeedCategoryAsync(), price: 10m);
        await SeedProductAsync(await SeedCategoryAsync(), price: 1000m);

        var result = await _harness.RecommendationService.GetRecommendationsAsync(source.Id, 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task The_result_is_capped_at_the_requested_count()
    {
        var category = await SeedCategoryAsync();
        var source = await SeedProductAsync(category);
        for (var i = 0; i < 5; i++)
        {
            await SeedProductAsync(category, name: $"Match {i}");
        }

        var result = await _harness.RecommendationService.GetRecommendationsAsync(source.Id, 3);

        result.Should().HaveCount(3);
    }

    private async Task<Category> SeedCategoryAsync()
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();
        return category;
    }

    private async Task<Brand> SeedBrandAsync()
    {
        var brand = new Brand { Name = "Brand", Slug = $"brand-{Guid.NewGuid():N}", IsActive = true };
        _harness.DbContext.Brands.Add(brand);
        await _harness.DbContext.SaveChangesAsync();
        return brand;
    }

    private async Task<Product> SeedProductAsync(
        Category category, Brand? brand = null, decimal price = 10m, bool isActive = true, bool isPublished = true, string name = "Widget")
    {
        var product = new Product
        {
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            BrandId = brand?.Id,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = price / 2,
            SellingPrice = price,
            IsActive = isActive,
            IsPublished = isPublished,
            PublishedAtUtc = DateTime.UtcNow,
        };
        _harness.DbContext.Products.Add(product);
        await _harness.DbContext.SaveChangesAsync();
        return product;
    }

    private async Task TagProductAsync(int productId, int tagId)
    {
        _harness.DbContext.Set<ProductTagMapping>().Add(new ProductTagMapping { ProductId = productId, ProductTagId = tagId });
        await _harness.DbContext.SaveChangesAsync();
    }
}
