using ECommerceApp.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.Property(p => p.OrderNumber).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(2000);
        builder.Property(p => p.ApprovedByUserId).HasMaxLength(450);
        builder.Property(p => p.CancelledByUserId).HasMaxLength(450);

        builder.HasIndex(p => p.OrderNumber).IsUnique();
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Warehouse)
            .WithMany()
            .HasForeignKey(p => p.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.ProductSku).HasMaxLength(100).IsRequired();
        builder.Property(i => i.UnitCost).HasColumnType("decimal(18,2)");

        builder.HasOne(i => i.PurchaseOrder)
            .WithMany(p => p.Items)
            .HasForeignKey(i => i.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.PurchaseOrderId);
    }
}

public class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.Property(r => r.ReceivedByUserId).HasMaxLength(450);
        builder.Property(r => r.Notes).HasMaxLength(2000);
        builder.Property(r => r.OverrideReason).HasMaxLength(500);

        // Restrict: the receiving history must survive even though there is no
        // path today that deletes a PurchaseOrder - same reasoning as StockMovements.
        builder.HasOne(r => r.PurchaseOrder)
            .WithMany()
            .HasForeignKey(r => r.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.PurchaseOrderId);
        builder.HasIndex(r => r.ReceivedAtUtc);
    }
}

public class GoodsReceiptItemConfiguration : IEntityTypeConfiguration<GoodsReceiptItem>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptItem> builder)
    {
        builder.HasOne(i => i.GoodsReceipt)
            .WithMany(r => r.Items)
            .HasForeignKey(i => i.GoodsReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.PurchaseOrderItem)
            .WithMany()
            .HasForeignKey(i => i.PurchaseOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.GoodsReceiptId);
        builder.HasIndex(i => i.PurchaseOrderItemId);
    }
}
