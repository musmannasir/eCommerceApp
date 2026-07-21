using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Tests.Catalog;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Wishlist;

public class WishlistServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Toggling_an_unwishlisted_product_adds_it()
    {
        var product = await SeedProductAsync();

        var result = await _harness.WishlistService.ToggleAsync("user-1", product.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsWishlisted.Should().BeTrue();
        result.Value.ItemCount.Should().Be(1);
    }

    [Fact]
    public async Task Toggling_an_already_wishlisted_product_removes_it()
    {
        var product = await SeedProductAsync();
        await _harness.WishlistService.ToggleAsync("user-1", product.Id);

        var result = await _harness.WishlistService.ToggleAsync("user-1", product.Id);

        result.Value.IsWishlisted.Should().BeFalse();
        result.Value.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task Toggling_an_unpublished_product_is_not_found()
    {
        var product = await SeedProductAsync(isPublished: false);

        var result = await _harness.WishlistService.ToggleAsync("user-1", product.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Toggling_a_nonexistent_product_is_not_found()
    {
        var result = await _harness.WishlistService.ToggleAsync("user-1", 999999);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task The_wishlist_orders_items_most_recently_added_first()
    {
        var first = await SeedProductAsync(name: "First");
        var second = await SeedProductAsync(name: "Second");
        await _harness.WishlistService.ToggleAsync("user-1", first.Id);
        _harness.Clock.UtcNow = _harness.Clock.UtcNow.AddMinutes(1);
        await _harness.WishlistService.ToggleAsync("user-1", second.Id);

        var wishlist = await _harness.WishlistService.GetWishlistAsync("user-1");

        wishlist.Items.Should().HaveCount(2);
        wishlist.Items[0].Product.Id.Should().Be(second.Id);
        wishlist.Items[1].Product.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task A_product_that_became_unpublished_since_being_wishlisted_is_excluded()
    {
        var product = await SeedProductAsync();
        await _harness.WishlistService.ToggleAsync("user-1", product.Id);

        product.IsPublished = false;
        await _harness.DbContext.SaveChangesAsync();

        var wishlist = await _harness.WishlistService.GetWishlistAsync("user-1");

        wishlist.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task IsWishlistedAsync_reflects_current_state()
    {
        var product = await SeedProductAsync();

        (await _harness.WishlistService.IsWishlistedAsync("user-1", product.Id)).Should().BeFalse();

        await _harness.WishlistService.ToggleAsync("user-1", product.Id);

        (await _harness.WishlistService.IsWishlistedAsync("user-1", product.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Removing_an_item_is_idempotent()
    {
        var product = await SeedProductAsync();
        await _harness.WishlistService.ToggleAsync("user-1", product.Id);

        var first = await _harness.WishlistService.RemoveItemAsync("user-1", product.Id);
        var second = await _harness.WishlistService.RemoveItemAsync("user-1", product.Id);

        first.ItemCount.Should().Be(0);
        second.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task Two_different_users_have_isolated_wishlists()
    {
        var product = await SeedProductAsync();
        await _harness.WishlistService.ToggleAsync("user-1", product.Id);

        var otherUsersWishlist = await _harness.WishlistService.GetWishlistAsync("user-2");

        otherUsersWishlist.Items.Should().BeEmpty();
    }

    private async Task<Category> SeedCategoryAsync()
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();
        return category;
    }

    private async Task<Product> SeedProductAsync(bool isActive = true, bool isPublished = true, string name = "Widget")
    {
        var category = await SeedCategoryAsync();
        var product = new Product
        {
            Name = name,
            Slug = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = 5,
            SellingPrice = 10,
            IsActive = isActive,
            IsPublished = isPublished,
            PublishedAtUtc = DateTime.UtcNow,
        };
        _harness.DbContext.Products.Add(product);
        await _harness.DbContext.SaveChangesAsync();
        return product;
    }
}
