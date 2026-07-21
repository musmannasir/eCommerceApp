using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Inventory;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Inventory;

/// <summary>
/// Verifies InventoryItem's RowVersion is a real, SQL-Server-enforced concurrency
/// token, matching the same guarantee Milestone 2 established for catalog
/// entities (see Catalog/ConcurrencyTests.cs) - critical here because two staff
/// members adjusting the same item's stock concurrently must not silently
/// overwrite one another's change.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class InventoryConcurrencyTests
{
    private readonly AuthTestFixture _fixture;

    public InventoryConcurrencyTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Saving_a_stale_copy_of_an_inventory_item_throws_a_concurrency_exception()
    {
        int inventoryItemId;
        using (var setupScope = _fixture.Factory.Services.CreateScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var category = new Category { Name = "Concurrency Cat", Slug = $"concurrency-cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
            setupContext.Categories.Add(category);
            await setupContext.SaveChangesAsync();

            var product = new Product
            {
                Name = "Concurrency Widget",
                Slug = $"concurrency-widget-{Guid.NewGuid():N}",
                CategoryId = category.Id,
                BaseSKU = $"SKU-{Guid.NewGuid():N}",
                CostPrice = 5,
                SellingPrice = 10,
                IsActive = true,
            };
            setupContext.Products.Add(product);

            var warehouse = new Warehouse { Name = "Concurrency WH", Code = $"WH-{Guid.NewGuid():N}", IsActive = true };
            setupContext.Warehouses.Add(warehouse);
            await setupContext.SaveChangesAsync();

            var item = new InventoryItem
            {
                WarehouseId = warehouse.Id,
                ProductId = product.Id,
                QuantityOnHand = 10,
                QuantityReserved = 0,
                ReorderLevel = 2,
                ReorderQuantity = 5,
                AllowBackorder = false,
                StockStatus = StockStatus.InStock,
                LastStockUpdateUtc = DateTime.UtcNow,
            };
            setupContext.InventoryItems.Add(item);
            await setupContext.SaveChangesAsync();

            inventoryItemId = item.Id;
        }

        using var scopeA = _fixture.Factory.Services.CreateScope();
        using var scopeB = _fixture.Factory.Services.CreateScope();
        var contextA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contextB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var itemA = await contextA.InventoryItems.SingleAsync(i => i.Id == inventoryItemId);
        var itemB = await contextB.InventoryItems.SingleAsync(i => i.Id == inventoryItemId);

        itemA.QuantityOnHand = 25;
        await contextA.SaveChangesAsync();

        itemB.QuantityOnHand = 30;
        var act = async () => await contextB.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
