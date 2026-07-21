using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Catalog;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

/// <summary>Milestone 4.3: filters (individually and combined), sorting, and search
/// matching beyond the baseline M4.2 substring match already covered elsewhere.</summary>
public class CatalogBrowseFilterTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Searching_matches_brand_name()
    {
        var category = await SeedCategoryAsync();
        var brand = new Brand { Name = "Zentron", Slug = "zentron", IsActive = true };
        _harness.DbContext.Brands.Add(brand);
        await _harness.DbContext.SaveChangesAsync();
        var product = SeedProduct(category.Id, "Generic Widget", brandId: brand.Id);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.Search, SearchTerm = "Zentron" });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == product.Id);
    }

    [Fact]
    public async Task Searching_matches_category_name()
    {
        var category = new Category { Name = "Gardening Tools", Slug = "gardening-tools", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();
        var product = SeedProduct(category.Id, "Generic Widget");
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.Search, SearchTerm = "Gardening" });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == product.Id);
    }

    [Theory]
    [InlineData("100% Cotton")]
    [InlineData("O'Brien's")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("a_b")]
    public async Task Special_characters_in_the_search_term_do_not_throw(string term)
    {
        var category = await SeedCategoryAsync();
        SeedProduct(category.Id, "Generic Widget");
        await _harness.DbContext.SaveChangesAsync();

        var act = async () => await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.Search, SearchTerm = term });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Price_range_filters_products_outside_the_range()
    {
        var category = await SeedCategoryAsync();
        var cheap = SeedProduct(category.Id, "Cheap Widget", sellingPrice: 5m);
        var mid = SeedProduct(category.Id, "Mid Widget", sellingPrice: 15m);
        var expensive = SeedProduct(category.Id, "Expensive Widget", sellingPrice: 50m);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, MinPrice = 10m, MaxPrice = 20m });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == mid.Id);
        result.Value.Products.Items.Should().NotContain(p => p.Id == cheap.Id || p.Id == expensive.Id);
    }

    [Fact]
    public async Task Category_filter_narrows_to_that_category_and_its_descendants()
    {
        var parent = new Category { Name = "Electronics", Slug = "electronics", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(parent);
        await _harness.DbContext.SaveChangesAsync();
        var other = await SeedCategoryAsync();

        var inCategory = SeedProduct(parent.Id, "TV");
        var outOfCategory = SeedProduct(other.Id, "Shirt");
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, FilterCategoryId = parent.Id });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == inCategory.Id);
        result.Value.Products.Items.Should().NotContain(p => p.Id == outOfCategory.Id);
    }

    [Fact]
    public async Task Brand_filter_narrows_to_that_brand()
    {
        var category = await SeedCategoryAsync();
        var brand = new Brand { Name = "Acme", Slug = "acme", IsActive = true };
        _harness.DbContext.Brands.Add(brand);
        await _harness.DbContext.SaveChangesAsync();
        var matching = SeedProduct(category.Id, "Branded Widget", brandId: brand.Id);
        var nonMatching = SeedProduct(category.Id, "Unbranded Widget");
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, FilterBrandId = brand.Id });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == matching.Id);
        result.Value.Products.Items.Should().NotContain(p => p.Id == nonMatching.Id);
    }

    [Fact]
    public async Task Featured_filter_only_returns_featured_products()
    {
        var category = await SeedCategoryAsync();
        var featured = SeedProduct(category.Id, "Featured Widget", isFeatured: true);
        var plain = SeedProduct(category.Id, "Plain Widget");
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, OnlyFeatured = true });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == featured.Id);
        result.Value.Products.Items.Should().NotContain(p => p.Id == plain.Id);
    }

    [Fact]
    public async Task Discounted_filter_only_returns_products_with_a_real_discount()
    {
        var category = await SeedCategoryAsync();
        var discounted = SeedProduct(category.Id, "Discounted Widget", sellingPrice: 8m, compareAtPrice: 10m);
        var fullPrice = SeedProduct(category.Id, "Full Price Widget", sellingPrice: 10m);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, OnlyDiscounted = true });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == discounted.Id);
        result.Value.Products.Items.Should().NotContain(p => p.Id == fullPrice.Id);
    }

    [Fact]
    public async Task New_arrivals_filter_excludes_products_published_outside_the_window()
    {
        var category = await SeedCategoryAsync();
        var recent = SeedProduct(category.Id, "New Widget");
        recent.PublishedAtUtc = DateTime.UtcNow.AddDays(-1);
        var old = SeedProduct(category.Id, "Old Widget");
        old.PublishedAtUtc = DateTime.UtcNow.AddDays(-90);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, OnlyNewArrivals = true });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == recent.Id);
        result.Value.Products.Items.Should().NotContain(p => p.Id == old.Id);
    }

    [Fact]
    public async Task Attribute_filter_only_returns_products_with_a_matching_variant()
    {
        var category = await SeedCategoryAsync();
        var attribute = new ProductAttribute { Name = "Color" };
        _harness.DbContext.ProductAttributes.Add(attribute);
        await _harness.DbContext.SaveChangesAsync();
        var red = new ProductAttributeValue { ProductAttributeId = attribute.Id, Value = "Red" };
        var blue = new ProductAttributeValue { ProductAttributeId = attribute.Id, Value = "Blue" };
        _harness.DbContext.ProductAttributeValues.AddRange(red, blue);
        await _harness.DbContext.SaveChangesAsync();

        var redProduct = SeedProduct(category.Id, "Red Widget");
        var blueProduct = SeedProduct(category.Id, "Blue Widget");
        await _harness.DbContext.SaveChangesAsync();

        var redVariant = new ProductVariant { ProductId = redProduct.Id, SKU = $"SKU-{Guid.NewGuid():N}", CombinationKey = red.Id.ToString() };
        var blueVariant = new ProductVariant { ProductId = blueProduct.Id, SKU = $"SKU-{Guid.NewGuid():N}", CombinationKey = blue.Id.ToString() };
        _harness.DbContext.ProductVariants.AddRange(redVariant, blueVariant);
        await _harness.DbContext.SaveChangesAsync();

        _harness.DbContext.Set<ProductVariantAttributeValue>().AddRange(
            new ProductVariantAttributeValue { ProductVariantId = redVariant.Id, ProductAttributeValueId = red.Id },
            new ProductVariantAttributeValue { ProductVariantId = blueVariant.Id, ProductAttributeValueId = blue.Id });
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, AttributeValueIds = new[] { red.Id } });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == redProduct.Id);
        result.Value.Products.Items.Should().NotContain(p => p.Id == blueProduct.Id);
    }

    [Fact]
    public async Task Combined_filters_apply_together()
    {
        var category = await SeedCategoryAsync();
        var brand = new Brand { Name = "Acme", Slug = "acme", IsActive = true };
        _harness.DbContext.Brands.Add(brand);
        await _harness.DbContext.SaveChangesAsync();

        var matches = SeedProduct(category.Id, "Matches Everything", brandId: brand.Id, isFeatured: true, sellingPrice: 15m);
        var wrongBrand = SeedProduct(category.Id, "Wrong Brand", isFeatured: true, sellingPrice: 15m);
        var notFeatured = SeedProduct(category.Id, "Not Featured", brandId: brand.Id, sellingPrice: 15m);
        var wrongPrice = SeedProduct(category.Id, "Wrong Price", brandId: brand.Id, isFeatured: true, sellingPrice: 999m);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery
        {
            Mode = CatalogBrowseMode.All,
            FilterBrandId = brand.Id,
            OnlyFeatured = true,
            MinPrice = 10m,
            MaxPrice = 20m,
        });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == matches.Id);
        result.Value.Products.Items.Should().NotContain(p => p.Id == wrongBrand.Id || p.Id == notFeatured.Id || p.Id == wrongPrice.Id);
    }

    [Fact]
    public async Task Sorting_by_price_ascending_orders_lowest_first()
    {
        var category = await SeedCategoryAsync();
        SeedProduct(category.Id, "B Widget", sellingPrice: 20m);
        SeedProduct(category.Id, "A Widget", sellingPrice: 5m);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, Sort = CatalogSortOption.PriceAsc });

        result.Value.Products.Items.Select(p => p.SellingPrice).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Sorting_by_price_descending_orders_highest_first()
    {
        var category = await SeedCategoryAsync();
        SeedProduct(category.Id, "A Widget", sellingPrice: 5m);
        SeedProduct(category.Id, "B Widget", sellingPrice: 20m);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, Sort = CatalogSortOption.PriceDesc });

        result.Value.Products.Items.Select(p => p.SellingPrice).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Sorting_by_name_ascending_orders_alphabetically()
    {
        var category = await SeedCategoryAsync();
        SeedProduct(category.Id, "Zebra Widget");
        SeedProduct(category.Id, "Apple Widget");
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, Sort = CatalogSortOption.NameAsc });

        result.Value.Products.Items.Select(p => p.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Sorting_by_largest_discount_puts_the_biggest_discount_first()
    {
        var category = await SeedCategoryAsync();
        var smallDiscount = SeedProduct(category.Id, "Small Discount", sellingPrice: 9m, compareAtPrice: 10m);
        var bigDiscount = SeedProduct(category.Id, "Big Discount", sellingPrice: 5m, compareAtPrice: 20m);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, Sort = CatalogSortOption.LargestDiscount });

        result.Value.Products.Items.First().Id.Should().Be(bigDiscount.Id);
    }

    [Fact]
    public async Task Sorting_by_newest_puts_the_most_recently_published_first()
    {
        var category = await SeedCategoryAsync();
        var old = SeedProduct(category.Id, "Old Widget");
        old.PublishedAtUtc = DateTime.UtcNow.AddDays(-30);
        var recent = SeedProduct(category.Id, "New Widget");
        recent.PublishedAtUtc = DateTime.UtcNow;
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, Sort = CatalogSortOption.Newest });

        result.Value.Products.Items.First().Id.Should().Be(recent.Id);
    }

    [Fact]
    public async Task Relevance_sort_prioritizes_a_search_term_appearing_at_the_start_of_the_name()
    {
        var category = await SeedCategoryAsync();
        var containsMatch = SeedProduct(category.Id, "Blue Widget Deluxe");
        var startsWithMatch = SeedProduct(category.Id, "Widget Basic");
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery
        {
            Mode = CatalogBrowseMode.Search,
            SearchTerm = "Widget",
            Sort = CatalogSortOption.Relevance,
        });

        result.Value.Products.Items.First().Id.Should().Be(startsWithMatch.Id);
        result.Value.Products.Items.Should().Contain(p => p.Id == containsMatch.Id);
    }

    [Fact]
    public async Task GetSuggestionsAsync_returns_matching_products()
    {
        var category = await SeedCategoryAsync();
        var product = SeedProduct(category.Id, "Suggested Widget");
        await _harness.DbContext.SaveChangesAsync();

        var suggestions = await _harness.CatalogBrowseService.GetSuggestionsAsync("Suggested");

        suggestions.Should().ContainSingle(s => s.Name == product.Name);
    }

    [Fact]
    public async Task GetSuggestionsAsync_returns_nothing_for_a_blank_term()
    {
        var suggestions = await _harness.CatalogBrowseService.GetSuggestionsAsync("   ");

        suggestions.Should().BeEmpty();
    }

    private async Task<Category> SeedCategoryAsync()
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();
        return category;
    }

    private Product SeedProduct(
        int categoryId, string name, int? brandId = null, bool isFeatured = false,
        decimal sellingPrice = 10m, decimal? compareAtPrice = null)
    {
        var product = new Product
        {
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
            CategoryId = categoryId,
            BrandId = brandId,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = 5,
            SellingPrice = sellingPrice,
            CompareAtPrice = compareAtPrice,
            IsActive = true,
            IsPublished = true,
            IsFeatured = isFeatured,
            PublishedAtUtc = DateTime.UtcNow,
        };
        _harness.DbContext.Products.Add(product);
        return product;
    }
}
