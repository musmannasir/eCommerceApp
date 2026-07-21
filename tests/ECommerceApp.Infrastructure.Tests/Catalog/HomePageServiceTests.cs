using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Marketing;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class HomePageServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Unpublished_products_never_appear_in_any_home_page_section()
    {
        var category = await SeedCategoryAsync();
        SeedProduct(category.Id, "Unpublished Featured", isActive: true, isPublished: false, isFeatured: true);
        await _harness.DbContext.SaveChangesAsync();

        var homePage = await _harness.HomePageService.GetHomePageAsync();

        homePage.FeaturedProducts.Should().BeEmpty();
        homePage.NewArrivals.Should().BeEmpty();
    }

    [Fact]
    public async Task Inactive_products_never_appear_in_any_home_page_section()
    {
        var category = await SeedCategoryAsync();
        SeedProduct(category.Id, "Inactive Featured", isActive: false, isPublished: true, isFeatured: true);
        await _harness.DbContext.SaveChangesAsync();

        var homePage = await _harness.HomePageService.GetHomePageAsync();

        homePage.FeaturedProducts.Should().BeEmpty();
        homePage.NewArrivals.Should().BeEmpty();
    }

    [Fact]
    public async Task Featured_and_published_active_products_appear_in_featured_products()
    {
        var category = await SeedCategoryAsync();
        var product = SeedProduct(category.Id, "Featured Widget", isActive: true, isPublished: true, isFeatured: true);
        await _harness.DbContext.SaveChangesAsync();

        var homePage = await _harness.HomePageService.GetHomePageAsync();

        homePage.FeaturedProducts.Should().ContainSingle(p => p.Id == product.Id);
    }

    [Fact]
    public async Task Non_featured_published_products_do_not_appear_in_featured_products_but_do_in_new_arrivals()
    {
        var category = await SeedCategoryAsync();
        var product = SeedProduct(category.Id, "Plain Widget", isActive: true, isPublished: true, isFeatured: false);
        await _harness.DbContext.SaveChangesAsync();

        var homePage = await _harness.HomePageService.GetHomePageAsync();

        homePage.FeaturedProducts.Should().NotContain(p => p.Id == product.Id);
        homePage.NewArrivals.Should().ContainSingle(p => p.Id == product.Id);
    }

    [Fact]
    public async Task Products_with_a_compare_at_price_above_selling_price_appear_as_discounted()
    {
        var category = await SeedCategoryAsync();
        var discounted = SeedProduct(category.Id, "Discounted Widget", isActive: true, isPublished: true, isFeatured: false, compareAtPrice: 20m, sellingPrice: 15m);
        var fullPrice = SeedProduct(category.Id, "Full Price Widget", isActive: true, isPublished: true, isFeatured: false);
        await _harness.DbContext.SaveChangesAsync();

        var homePage = await _harness.HomePageService.GetHomePageAsync();

        homePage.DiscountedProducts.Should().ContainSingle(p => p.Id == discounted.Id);
        homePage.DiscountedProducts.Should().NotContain(p => p.Id == fullPrice.Id);
        homePage.DiscountedProducts.Single(p => p.Id == discounted.Id).DiscountPercent.Should().Be(25);
    }

    [Fact]
    public async Task Featured_categories_are_active_and_featured_only()
    {
        var featuredActive = new Category { Name = "Featured", Slug = "featured-cat", DisplayOrder = 0, IsActive = true, IsFeatured = true };
        var featuredInactive = new Category { Name = "Featured Inactive", Slug = "featured-inactive-cat", DisplayOrder = 0, IsActive = false, IsFeatured = true };
        var notFeatured = new Category { Name = "Plain", Slug = "plain-cat", DisplayOrder = 0, IsActive = true, IsFeatured = false };
        _harness.DbContext.Categories.AddRange(featuredActive, featuredInactive, notFeatured);
        await _harness.DbContext.SaveChangesAsync();

        var homePage = await _harness.HomePageService.GetHomePageAsync();

        homePage.FeaturedCategories.Should().ContainSingle(c => c.Id == featuredActive.Id);
    }

    [Fact]
    public async Task Only_active_hero_and_promo_banners_with_an_image_appear_in_their_own_sections()
    {
        _harness.DbContext.HomePageBanners.AddRange(
            new HomePageBanner { Title = "Hero 1", BannerType = BannerType.Hero, IsActive = true, DisplayOrder = 0, ImagePath = "/uploads/home-banners/hero1.jpg" },
            new HomePageBanner { Title = "Inactive Hero", BannerType = BannerType.Hero, IsActive = false, DisplayOrder = 1, ImagePath = "/uploads/home-banners/hero2.jpg" },
            new HomePageBanner { Title = "Imageless Hero", BannerType = BannerType.Hero, IsActive = true, DisplayOrder = 2, ImagePath = null },
            new HomePageBanner { Title = "Promo 1", BannerType = BannerType.Promo, IsActive = true, DisplayOrder = 0, ImagePath = "/uploads/home-banners/promo1.jpg" });
        await _harness.DbContext.SaveChangesAsync();

        var homePage = await _harness.HomePageService.GetHomePageAsync();

        homePage.HeroBanners.Should().ContainSingle(b => b.Title == "Hero 1");
        homePage.PromoBanners.Should().ContainSingle(b => b.Title == "Promo 1");
    }

    private async Task<Category> SeedCategoryAsync()
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();
        return category;
    }

    private Product SeedProduct(
        int categoryId, string name, bool isActive, bool isPublished, bool isFeatured,
        decimal sellingPrice = 10m, decimal? compareAtPrice = null)
    {
        var product = new Product
        {
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
            CategoryId = categoryId,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = 5,
            SellingPrice = sellingPrice,
            CompareAtPrice = compareAtPrice,
            IsActive = isActive,
            IsPublished = isPublished,
            IsFeatured = isFeatured,
            PublishedAtUtc = isPublished ? DateTime.UtcNow : null,
        };
        _harness.DbContext.Products.Add(product);
        return product;
    }
}
