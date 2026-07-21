using ECommerceApp.Infrastructure.Inventory;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Tests.Inventory;

/// <summary>Wires up InventoryService against an EF Core InMemory-backed context - no real DB needed.</summary>
public sealed class InventoryTestHarness : IDisposable
{
    public TestDbContext DbContext { get; }
    public FakeClock Clock { get; }
    public FakeCurrentUserService CurrentUser { get; }
    public InventoryService InventoryService { get; }
    public SupplierService SupplierService { get; }
    public PurchaseOrderService PurchaseOrderService { get; }

    public InventoryTestHarness()
    {
        var options = new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Clock = new FakeClock();
        CurrentUser = new FakeCurrentUserService();
        DbContext = new TestDbContext(options, CurrentUser, Clock);

        InventoryService = new InventoryService(DbContext, Clock, CurrentUser);
        SupplierService = new SupplierService(DbContext);
        PurchaseOrderService = new PurchaseOrderService(DbContext, Clock, CurrentUser);
    }

    public void Dispose() => DbContext.Dispose();
}
