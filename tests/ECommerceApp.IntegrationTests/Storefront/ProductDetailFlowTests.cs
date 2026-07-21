using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Storefront;

/// <summary>
/// Drives the public product detail page over real HTTP against the real SQL
/// Server test database - the related-products card projection reuses the
/// same inline Expression&lt;Func&lt;Product, HomeProductCardDto&gt;&gt; pattern as
/// the other Storefront services specifically so it's guaranteed translatable;
/// this is the test that proves it for this endpoint too. Also covers the
/// Milestone 5.2 live-resolution endpoint the same way.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class ProductDetailFlowTests
{
    private readonly AuthTestFixture _fixture;

    public ProductDetailFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task An_active_published_product_with_variants_and_related_products_renders_successfully()
    {
        var (productSlug, _, suffix) = await SeedProductWithVariantAsync();

        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/Product/{productSlug}");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(because: body);
        body.Should().Contain($"Widget {suffix}");
        body.Should().Contain($"Related Widget {suffix}");
    }

    [Fact]
    public async Task An_unknown_product_slug_returns_404()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Product/does-not-exist");

        ((int)response.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task The_resolve_endpoint_returns_the_variants_authoritative_data_against_real_SQL_Server()
    {
        var (productSlug, variantId, suffix) = await SeedProductWithVariantAsync();
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/Product/{productSlug}/Resolve?variantId={variantId}");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(because: body);
        body.Should().Contain($"SKU-VAR-{suffix}");
    }

    [Fact]
    public async Task The_resolve_endpoint_rejects_a_variant_id_that_does_not_belong_to_the_product()
    {
        var (productSlug, _, _) = await SeedProductWithVariantAsync();
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/Product/{productSlug}/Resolve?variantId=999999");

        ((int)response.StatusCode).Should().Be(404);
    }

    private async Task<(string ProductSlug, int VariantId, string Suffix)> SeedProductWithVariantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var productSlug = $"widget-{suffix}";
        int variantId;

        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var category = new Category { Name = $"Category {suffix}", Slug = $"cat-{suffix}", DisplayOrder = 0, IsActive = true };
        dbContext.Categories.Add(category);
        var brand = new Brand { Name = $"Brand {suffix}", Slug = $"brand-{suffix}", IsActive = true };
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = new Product
        {
            Name = $"Widget {suffix}",
            Slug = productSlug,
            CategoryId = category.Id,
            BrandId = brand.Id,
            BaseSKU = $"SKU-{suffix}",
            CostPrice = 5,
            SellingPrice = 19.99m,
            CompareAtPrice = 24.99m,
            ShortDescription = "A great widget.",
            IsActive = true,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
        };
        dbContext.Products.Add(product);

        var related = new Product
        {
            Name = $"Related Widget {suffix}",
            Slug = $"related-widget-{suffix}",
            CategoryId = category.Id,
            BaseSKU = $"SKU-REL-{suffix}",
            CostPrice = 5,
            SellingPrice = 9.99m,
            IsActive = true,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
        };
        dbContext.Products.Add(related);
        await dbContext.SaveChangesAsync();

        var attribute = new ProductAttribute { Name = $"Color-{suffix}" };
        dbContext.ProductAttributes.Add(attribute);
        await dbContext.SaveChangesAsync();

        var value = new ProductAttributeValue { ProductAttributeId = attribute.Id, Value = "Red" };
        dbContext.ProductAttributeValues.Add(value);
        await dbContext.SaveChangesAsync();

        var variant = new ProductVariant
        {
            ProductId = product.Id,
            SKU = $"SKU-VAR-{suffix}",
            CombinationKey = ProductVariant.BuildCombinationKey(new[] { value.Id }),
            IsActive = true,
        };
        dbContext.ProductVariants.Add(variant);
        await dbContext.SaveChangesAsync();
        variantId = variant.Id;

        dbContext.Set<ProductVariantAttributeValue>().Add(new ProductVariantAttributeValue
        {
            ProductVariantId = variant.Id,
            ProductAttributeValueId = value.Id,
        });
        await dbContext.SaveChangesAsync();

        return (productSlug, variantId, suffix);
    }
}
