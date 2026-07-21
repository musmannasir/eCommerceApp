using ECommerceApp.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        // QuantityAvailable = QuantityOnHand - QuantityReserved is computed in the
        // domain entity, not persisted, so it can never drift from its inputs.
        builder.Ignore(i => i.QuantityAvailable);

        // Restrict (not Cascade) on all three: deleting a warehouse/product/variant
        // must not silently destroy stock history - the app must handle that explicitly.
        builder.HasOne(i => i.Warehouse)
            .WithMany(w => w.InventoryItems)
            .HasForeignKey(i => i.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ProductVariant)
            .WithMany()
            .HasForeignKey(i => i.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        // One inventory record per purchasable unit per warehouse: a simple product
        // (no ProductVariantId) is tracked at the Product level; once a product has
        // variants, stock is tracked per-variant instead - never both at once for the
        // same product in the same warehouse. Two filtered unique indexes enforce this.
        builder.HasIndex(i => new { i.WarehouseId, i.ProductId })
            .IsUnique()
            .HasFilter("[ProductVariantId] IS NULL");

        builder.HasIndex(i => new { i.WarehouseId, i.ProductVariantId })
            .IsUnique()
            .HasFilter("[ProductVariantId] IS NOT NULL");

        builder.HasIndex(i => i.StockStatus);
    }
}
