using ECommerceApp.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.Property(m => m.ReferenceType).HasMaxLength(100);
        builder.Property(m => m.Reason).HasMaxLength(500);
        builder.Property(m => m.CreatedByUserId).HasMaxLength(450);

        // Restrict: the ledger must survive even if the inventory item it describes
        // is later removed (it never is today - InventoryItem has no delete path -
        // but the FK behavior documents the intent either way).
        builder.HasOne(m => m.InventoryItem)
            .WithMany()
            .HasForeignKey(m => m.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.InventoryItemId);
        builder.HasIndex(m => m.OccurredAtUtc);
    }
}
