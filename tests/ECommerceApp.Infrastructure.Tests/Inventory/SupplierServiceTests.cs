using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Inventory;

public class SupplierServiceTests : IDisposable
{
    private readonly InventoryTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Creating_a_supplier_succeeds_with_valid_data()
    {
        var result = await _harness.SupplierService.CreateAsync(new CreateSupplierRequest(
            "Acme Supplies", "ACME", "Jane Doe", "jane@acme.test", "555-0100",
            null, null, null, null, null, null, "https://acme.test", null, true));

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Acme Supplies");
        result.Value.Code.Should().Be("ACME");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Creating_a_supplier_with_a_duplicate_code_is_rejected()
    {
        await _harness.SupplierService.CreateAsync(new CreateSupplierRequest(
            "Acme Supplies", "ACME", null, null, null, null, null, null, null, null, null, null, null, true));

        var result = await _harness.SupplierService.CreateAsync(new CreateSupplierRequest(
            "Acme Supplies 2", "ACME", null, null, null, null, null, null, null, null, null, null, null, true));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Updating_a_supplier_persists_changes()
    {
        var created = await _harness.SupplierService.CreateAsync(new CreateSupplierRequest(
            "Acme Supplies", "ACME", null, null, null, null, null, null, null, null, null, null, null, true));

        var result = await _harness.SupplierService.UpdateAsync(new UpdateSupplierRequest(
            created.Value.Id, "Acme Supplies Ltd", "ACME", "New Contact", null, null,
            null, null, null, null, null, null, null, null, true));

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Acme Supplies Ltd");
        result.Value.ContactName.Should().Be("New Contact");
    }

    [Fact]
    public async Task Updating_a_supplier_to_a_code_already_used_by_another_supplier_is_rejected()
    {
        await _harness.SupplierService.CreateAsync(new CreateSupplierRequest(
            "Acme", "ACME", null, null, null, null, null, null, null, null, null, null, null, true));
        var other = await _harness.SupplierService.CreateAsync(new CreateSupplierRequest(
            "Beta", "BETA", null, null, null, null, null, null, null, null, null, null, null, true));

        var result = await _harness.SupplierService.UpdateAsync(new UpdateSupplierRequest(
            other.Value.Id, "Beta", "ACME", null, null, null, null, null, null, null, null, null, null, null, true));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Deleting_a_supplier_soft_deletes_it_and_it_no_longer_appears_in_the_paged_list()
    {
        var created = await _harness.SupplierService.CreateAsync(new CreateSupplierRequest(
            "Acme", "ACME", null, null, null, null, null, null, null, null, null, null, null, true));

        var deleteResult = await _harness.SupplierService.DeleteAsync(created.Value.Id);
        deleteResult.IsSuccess.Should().BeTrue();

        var page = await _harness.SupplierService.GetPagedAsync(new PagedQuery());
        page.Value.Items.Should().NotContain(s => s.Id == created.Value.Id);

        var deletedPage = await _harness.SupplierService.GetPagedAsync(new PagedQuery { OnlyDeleted = true });
        deletedPage.Value.Items.Should().Contain(s => s.Id == created.Value.Id);
    }

    [Fact]
    public async Task Restoring_a_deleted_supplier_makes_it_visible_again()
    {
        var created = await _harness.SupplierService.CreateAsync(new CreateSupplierRequest(
            "Acme", "ACME", null, null, null, null, null, null, null, null, null, null, null, true));
        await _harness.SupplierService.DeleteAsync(created.Value.Id);

        var restoreResult = await _harness.SupplierService.RestoreAsync(created.Value.Id);
        restoreResult.IsSuccess.Should().BeTrue();

        var page = await _harness.SupplierService.GetPagedAsync(new PagedQuery());
        page.Value.Items.Should().Contain(s => s.Id == created.Value.Id);
    }

    [Fact]
    public async Task Deactivating_and_reactivating_a_supplier_updates_its_active_flag()
    {
        var created = await _harness.SupplierService.CreateAsync(new CreateSupplierRequest(
            "Acme", "ACME", null, null, null, null, null, null, null, null, null, null, null, true));

        await _harness.SupplierService.DeactivateAsync(created.Value.Id);
        var afterDeactivate = await _harness.SupplierService.GetByIdAsync(created.Value.Id);
        afterDeactivate.Value.IsActive.Should().BeFalse();

        await _harness.SupplierService.ActivateAsync(created.Value.Id);
        var afterActivate = await _harness.SupplierService.GetByIdAsync(created.Value.Id);
        afterActivate.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Linking_a_product_to_a_supplier_succeeds()
    {
        var supplierId = await SeedSupplierAsync();
        var productId = await SeedProductAsync();

        var result = await _harness.SupplierService.LinkProductAsync(new LinkSupplierProductRequest(
            supplierId, productId, "SUP-SKU-1", 4.50m, 7, true));

        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().Be(productId);
        result.Value.SupplierSku.Should().Be("SUP-SKU-1");
        result.Value.IsPreferred.Should().BeTrue();

        var linked = await _harness.SupplierService.GetLinkedProductsAsync(supplierId);
        linked.Value.Should().ContainSingle(l => l.ProductId == productId);
    }

    [Fact]
    public async Task Linking_the_same_product_to_a_supplier_twice_is_rejected()
    {
        var supplierId = await SeedSupplierAsync();
        var productId = await SeedProductAsync();
        await _harness.SupplierService.LinkProductAsync(new LinkSupplierProductRequest(supplierId, productId, null, null, null, false));

        var result = await _harness.SupplierService.LinkProductAsync(new LinkSupplierProductRequest(supplierId, productId, null, null, null, false));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Updating_a_supplier_product_link_persists_the_new_values()
    {
        var supplierId = await SeedSupplierAsync();
        var productId = await SeedProductAsync();
        var link = await _harness.SupplierService.LinkProductAsync(new LinkSupplierProductRequest(supplierId, productId, "OLD-SKU", 3m, 5, false));

        var result = await _harness.SupplierService.UpdateProductLinkAsync(new UpdateSupplierProductRequest(link.Value.Id, "NEW-SKU", 6m, 10, true));

        result.IsSuccess.Should().BeTrue();
        result.Value.SupplierSku.Should().Be("NEW-SKU");
        result.Value.CostPrice.Should().Be(6m);
        result.Value.IsPreferred.Should().BeTrue();
    }

    [Fact]
    public async Task Unlinking_a_product_removes_it_from_the_linked_list()
    {
        var supplierId = await SeedSupplierAsync();
        var productId = await SeedProductAsync();
        var link = await _harness.SupplierService.LinkProductAsync(new LinkSupplierProductRequest(supplierId, productId, null, null, null, false));

        var result = await _harness.SupplierService.UnlinkProductAsync(link.Value.Id);

        result.IsSuccess.Should().BeTrue();
        var linked = await _harness.SupplierService.GetLinkedProductsAsync(supplierId);
        linked.Value.Should().BeEmpty();
    }

    private async Task<int> SeedSupplierAsync()
    {
        var supplier = new ECommerceApp.Domain.Inventory.Supplier { Name = "Acme", Code = $"S-{Guid.NewGuid():N}", IsActive = true };
        _harness.DbContext.Suppliers.Add(supplier);
        await _harness.DbContext.SaveChangesAsync();
        return supplier.Id;
    }

    private async Task<int> SeedProductAsync()
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();

        var product = new Product
        {
            Name = "Widget",
            Slug = $"widget-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = 5,
            SellingPrice = 10,
            IsActive = true,
        };
        _harness.DbContext.Products.Add(product);
        await _harness.DbContext.SaveChangesAsync();

        return product.Id;
    }
}
