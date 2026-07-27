using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the public category/brand/search listing pages over real HTTP against
/// the real SQL Server test database - deliberately not just the InMemory-backed
/// CatalogBrowseServiceTests. The product-card projection composes several
/// EF Core subqueries (image lookup, out-of-stock check) inline specifically so
/// they're guaranteed translatable; this is the test that actually proves it,
/// the same lesson Milestone 2's AddVariantAsync bug taught (see Architecture.md).
/// </summary>
[Collection(AuthTestCollection.Name)]
public class CatalogBrowseFlowTests
{
    private readonly AuthTestFixture _fixture;

    public CatalogBrowseFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task All_products_category_and_brand_pages_render_successfully_against_real_SQL_Server()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        int categoryId;
        string categorySlug = $"cat-{suffix}";
        string brandSlug = $"brand-{suffix}";

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var category = new Category { Name = $"Category {suffix}", Slug = categorySlug, DisplayOrder = 0, IsActive = true };
            dbContext.Categories.Add(category);
            var brand = new Brand { Name = $"Brand {suffix}", Slug = brandSlug, IsActive = true };
            dbContext.Brands.Add(brand);
            await dbContext.SaveChangesAsync();
            categoryId = category.Id;

            var product = new Product
            {
                Name = $"Widget {suffix}",
                Slug = $"widget-{suffix}",
                CategoryId = category.Id,
                BrandId = brand.Id,
                BaseSKU = $"SKU-{suffix}",
                CostPrice = 5,
                SellingPrice = 19.99m,
                CompareAtPrice = 24.99m,
                IsActive = true,
                IsPublished = true,
                PublishedAtUtc = DateTime.UtcNow,
            };
            dbContext.Products.Add(product);
            await dbContext.SaveChangesAsync();
        }

        var client = _fixture.Factory.CreateClient();

        var productsResponse = await client.GetAsync("/Products?sort=Newest");
        var productsBody = await productsResponse.Content.ReadAsStringAsync();
        productsResponse.IsSuccessStatusCode.Should().BeTrue(because: productsBody);
        productsBody.Should().Contain($"Widget {suffix}");

        var categoryResponse = await client.GetAsync($"/Category/{categorySlug}");
        var categoryBody = await categoryResponse.Content.ReadAsStringAsync();
        categoryResponse.IsSuccessStatusCode.Should().BeTrue(because: categoryBody);
        categoryBody.Should().Contain($"Widget {suffix}");

        var brandResponse = await client.GetAsync($"/Brand/{brandSlug}");
        var brandBody = await brandResponse.Content.ReadAsStringAsync();
        brandResponse.IsSuccessStatusCode.Should().BeTrue(because: brandBody);
        brandBody.Should().Contain($"Widget {suffix}");

        var searchResponse = await client.GetAsync($"/Search?q=Widget%20{suffix}");
        var searchBody = await searchResponse.Content.ReadAsStringAsync();
        searchResponse.IsSuccessStatusCode.Should().BeTrue(because: searchBody);
        searchBody.Should().Contain($"Widget {suffix}");
    }

    [Fact]
    public async Task An_unknown_category_slug_returns_404()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Category/does-not-exist");

        ((int)response.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task An_empty_search_result_shows_the_empty_state()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Search?q=zzz-no-such-product-zzz");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("No products found");
    }

    [Fact]
    public async Task The_brands_index_page_renders_successfully()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Brands");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Theory]
    [InlineData("Relevance")]
    [InlineData("Newest")]
    [InlineData("PriceAsc")]
    [InlineData("PriceDesc")]
    [InlineData("LargestDiscount")]
    [InlineData("NameAsc")]
    [InlineData("NameDesc")]
    public async Task Every_sort_option_renders_successfully_against_real_SQL_Server(string sort)
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/Products?sort={sort}");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(because: body);
    }

    [Fact]
    public async Task A_fully_filtered_and_sorted_request_renders_successfully_against_real_SQL_Server()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync(
            "/Products?minPrice=1&maxPrice=1000&inStock=true&discounted=true&featured=true&newArrivals=true&sort=PriceAsc&view=list");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(because: body);
    }

    [Fact]
    public async Task The_suggestions_endpoint_returns_json_against_real_SQL_Server()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = new Category { Name = $"Category {suffix}", Slug = $"cat-{suffix}", DisplayOrder = 0, IsActive = true };
            dbContext.Categories.Add(category);
            await dbContext.SaveChangesAsync();

            dbContext.Products.Add(new Product
            {
                Name = $"Suggested Widget {suffix}",
                Slug = $"suggested-widget-{suffix}",
                CategoryId = category.Id,
                BaseSKU = $"SKU-{suffix}",
                CostPrice = 5,
                SellingPrice = 9.99m,
                IsActive = true,
                IsPublished = true,
                PublishedAtUtc = DateTime.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }

        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/Search/Suggestions?q=Suggested%20Widget%20{suffix}");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(because: body);
        body.Should().Contain($"Suggested Widget {suffix}");
    }
}
