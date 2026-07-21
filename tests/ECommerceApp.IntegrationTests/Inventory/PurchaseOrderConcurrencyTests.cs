using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Inventory;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Inventory;

/// <summary>
/// Verifies PurchaseOrderItem's RowVersion is a real, SQL-Server-enforced
/// concurrency token - two staff receiving against the same outstanding line
/// concurrently must not silently double-apply (or lose) a receipt.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class PurchaseOrderConcurrencyTests
{
    private readonly AuthTestFixture _fixture;

    public PurchaseOrderConcurrencyTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Saving_a_stale_copy_of_a_purchase_order_item_throws_a_concurrency_exception()
    {
        int purchaseOrderItemId;
        using (var setupScope = _fixture.Factory.Services.CreateScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var category = new Category { Name = "PO Concurrency Cat", Slug = $"po-concurrency-cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
            setupContext.Categories.Add(category);
            await setupContext.SaveChangesAsync();

            var product = new Product
            {
                Name = "PO Concurrency Widget",
                Slug = $"po-concurrency-widget-{Guid.NewGuid():N}",
                CategoryId = category.Id,
                BaseSKU = $"SKU-{Guid.NewGuid():N}",
                CostPrice = 5,
                SellingPrice = 10,
                IsActive = true,
            };
            setupContext.Products.Add(product);

            var warehouse = new Warehouse { Name = "PO Concurrency WH", Code = $"WH-{Guid.NewGuid():N}", IsActive = true };
            setupContext.Warehouses.Add(warehouse);

            var supplier = new Supplier { Name = "PO Concurrency Supplier", Code = $"S-{Guid.NewGuid():N}", IsActive = true };
            setupContext.Suppliers.Add(supplier);
            await setupContext.SaveChangesAsync();

            var order = new PurchaseOrder
            {
                SupplierId = supplier.Id,
                WarehouseId = warehouse.Id,
                OrderNumber = $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                Status = PurchaseOrderStatus.Approved,
            };
            setupContext.PurchaseOrders.Add(order);
            await setupContext.SaveChangesAsync();

            var item = new PurchaseOrderItem
            {
                PurchaseOrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSku = product.BaseSKU,
                QuantityOrdered = 10,
                QuantityReceived = 0,
                UnitCost = 5m,
            };
            setupContext.PurchaseOrderItems.Add(item);
            await setupContext.SaveChangesAsync();

            purchaseOrderItemId = item.Id;
        }

        using var scopeA = _fixture.Factory.Services.CreateScope();
        using var scopeB = _fixture.Factory.Services.CreateScope();
        var contextA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contextB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var itemA = await contextA.PurchaseOrderItems.SingleAsync(i => i.Id == purchaseOrderItemId);
        var itemB = await contextB.PurchaseOrderItems.SingleAsync(i => i.Id == purchaseOrderItemId);

        itemA.QuantityReceived = 6;
        await contextA.SaveChangesAsync();

        itemB.QuantityReceived = 4;
        var act = async () => await contextB.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
