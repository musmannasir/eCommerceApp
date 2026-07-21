using ECommerceApp.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.Property(a => a.Reason).HasMaxLength(500).IsRequired();
        builder.Property(a => a.AdjustedByUserId).HasMaxLength(450);

        builder.HasOne(a => a.InventoryItem)
            .WithMany()
            .HasForeignKey(a => a.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.InventoryItemId);
    }
}
