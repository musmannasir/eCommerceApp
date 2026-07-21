using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Inventory;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class CatalogBrowseServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Pagination_splits_results_across_pages()
    {
        var category = await SeedCategoryAsync();
        for (var i = 0; i < 5; i++)
        {
            SeedProduct(category.Id, $"Widget {i}", isActive: true, isPublished: true);
        }
        await _harness.DbContext.SaveChangesAsync();

        var page1 = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, Page = 1, PageSize = 2 });
        var page2 = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All, Page = 2, PageSize = 2 });

        page1.Value.Products.Items.Should().HaveCount(2);
        page2.Value.Products.Items.Should().HaveCount(2);
        page1.Value.Products.TotalCount.Should().Be(5);
        page1.Value.Products.TotalPages.Should().Be(3);
        page1.Value.Products.Items.Should().NotBeEquivalentTo(page2.Value.Products.Items);
    }

    [Fact]
    public async Task Unpublished_and_inactive_products_are_excluded_from_the_all_products_listing()
    {
        var category = await SeedCategoryAsync();
        SeedProduct(category.Id, "Unpublished", isActive: true, isPublished: false);
        SeedProduct(category.Id, "Inactive", isActive: false, isPublished: true);
        var visible = SeedProduct(category.Id, "Visible", isActive: true, isPublished: true);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == visible.Id);
    }

    [Fact]
    public async Task Out_of_stock_products_still_appear_in_the_listing_by_default()
    {
        var category = await SeedCategoryAsync();
        var product = SeedProduct(category.Id, "Sold Out Widget", isActive: true, isPublished: true);
        await _harness.DbContext.SaveChangesAsync();

        var warehouse = new Warehouse { Name = "Main", Code = $"WH-{Guid.NewGuid():N}", IsActive = true };
        _harness.DbContext.Warehouses.Add(warehouse);
        await _harness.DbContext.SaveChangesAsync();
        _harness.DbContext.InventoryItems.Add(new InventoryItem
        {
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            QuantityOnHand = 0,
            QuantityReserved = 0,
            ReorderLevel = 0,
            AllowBackorder = false,
            StockStatus = StockStatus.OutOfStock,
            LastStockUpdateUtc = DateTime.UtcNow,
        });
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All });

        var card = result.Value.Products.Items.Should().ContainSingle(p => p.Id == product.Id).Subject;
        card.IsOutOfStock.Should().BeTrue();
    }

    [Fact]
    public async Task A_product_with_available_stock_is_not_flagged_out_of_stock()
    {
        var category = await SeedCategoryAsync();
        var product = SeedProduct(category.Id, "In Stock Widget", isActive: true, isPublished: true);
        await _harness.DbContext.SaveChangesAsync();

        var warehouse = new Warehouse { Name = "Main", Code = $"WH-{Guid.NewGuid():N}", IsActive = true };
        _harness.DbContext.Warehouses.Add(warehouse);
        await _harness.DbContext.SaveChangesAsync();
        _harness.DbContext.InventoryItems.Add(new InventoryItem
        {
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            QuantityOnHand = 10,
            QuantityReserved = 0,
            ReorderLevel = 0,
            AllowBackorder = false,
            StockStatus = StockStatus.InStock,
            LastStockUpdateUtc = DateTime.UtcNow,
        });
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.All });

        result.Value.Products.Items.Single(p => p.Id == product.Id).IsOutOfStock.Should().BeFalse();
    }

    [Fact]
    public async Task Browsing_a_parent_category_includes_products_from_its_active_subcategories()
    {
        var parent = new Category { Name = "Electronics", Slug = "electronics", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(parent);
        await _harness.DbContext.SaveChangesAsync();

        var child = new Category { Name = "Phones", Slug = "phones", ParentCategoryId = parent.Id, DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(child);
        await _harness.DbContext.SaveChangesAsync();

        var childProduct = SeedProduct(child.Id, "Smartphone", isActive: true, isPublished: true);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.Category, CategorySlug = "electronics" });

        result.IsSuccess.Should().BeTrue();
        result.Value.Products.Items.Should().ContainSingle(p => p.Id == childProduct.Id);
    }

    [Fact]
    public async Task Browsing_a_nonexistent_category_slug_fails_with_not_found()
    {
        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.Category, CategorySlug = "does-not-exist" });

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Browsing_an_inactive_category_fails_with_not_found()
    {
        var category = new Category { Name = "Discontinued", Slug = "discontinued", DisplayOrder = 0, IsActive = false };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.Category, CategorySlug = "discontinued" });

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Browsing_a_brand_only_returns_that_brands_products()
    {
        var category = await SeedCategoryAsync();
        var brandA = new Brand { Name = "Acme", Slug = "acme", IsActive = true };
        var brandB = new Brand { Name = "Globex", Slug = "globex", IsActive = true };
        _harness.DbContext.Brands.AddRange(brandA, brandB);
        await _harness.DbContext.SaveChangesAsync();

        var acmeProduct = SeedProduct(category.Id, "Acme Widget", isActive: true, isPublished: true, brandId: brandA.Id);
        SeedProduct(category.Id, "Globex Widget", isActive: true, isPublished: true, brandId: brandB.Id);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.Brand, BrandSlug = "acme" });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == acmeProduct.Id);
        result.Value.BrandName.Should().Be("Acme");
    }

    [Fact]
    public async Task Browsing_a_nonexistent_brand_slug_fails_with_not_found()
    {
        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.Brand, BrandSlug = "does-not-exist" });

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Searching_matches_product_name()
    {
        var category = await SeedCategoryAsync();
        var match = SeedProduct(category.Id, "Wireless Mouse", isActive: true, isPublished: true);
        SeedProduct(category.Id, "USB Cable", isActive: true, isPublished: true);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.Search, SearchTerm = "Mouse" });

        result.Value.Products.Items.Should().ContainSingle(p => p.Id == match.Id);
    }

    [Fact]
    public async Task Searching_with_no_matches_returns_an_empty_result()
    {
        var category = await SeedCategoryAsync();
        SeedProduct(category.Id, "Wireless Mouse", isActive: true, isPublished: true);
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CatalogBrowseService.BrowseAsync(new CatalogBrowseQuery { Mode = CatalogBrowseMode.Search, SearchTerm = "nonexistent-term-xyz" });

        result.Value.Products.Items.Should().BeEmpty();
        result.Value.Products.TotalCount.Should().Be(0);
    }

    private async Task<Category> SeedCategoryAsync()
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();
        return category;
    }

    private Product SeedProduct(int categoryId, string name, bool isActive, bool isPublished, int? brandId = null)
    {
        var product = new Product
        {
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
            CategoryId = categoryId,
            BrandId = brandId,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = 5,
            SellingPrice = 10,
            IsActive = isActive,
            IsPublished = isPublished,
            PublishedAtUtc = isPublished ? DateTime.UtcNow : null,
        };
        _harness.DbContext.Products.Add(product);
        return product;
    }
}
