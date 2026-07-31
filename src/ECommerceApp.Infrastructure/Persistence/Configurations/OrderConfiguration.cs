using ECommerceApp.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
        builder.Property(o => o.UserId).HasMaxLength(450).IsRequired();
        builder.Property(o => o.IdempotencyKey).HasMaxLength(64).IsRequired();

        builder.Property(o => o.ShippingLabel).HasMaxLength(100);
        builder.Property(o => o.ShippingFullName).HasMaxLength(200).IsRequired();
        builder.Property(o => o.ShippingPhone).HasMaxLength(30).IsRequired();
        builder.Property(o => o.ShippingLine1).HasMaxLength(200).IsRequired();
        builder.Property(o => o.ShippingLine2).HasMaxLength(200);
        builder.Property(o => o.ShippingCity).HasMaxLength(100).IsRequired();
        builder.Property(o => o.ShippingRegionCode).HasMaxLength(10);
        builder.Property(o => o.ShippingPostalCode).HasMaxLength(20).IsRequired();
        builder.Property(o => o.ShippingCountryCode).HasMaxLength(2).IsRequired();

        builder.Property(o => o.ShippingMethodName).HasMaxLength(200).IsRequired();
        builder.Property(o => o.ShippingCost).HasColumnType("decimal(18,2)");

        builder.Property(o => o.AppliedCouponCode).HasMaxLength(50);
        builder.Property(o => o.AppliedPromotionName).HasMaxLength(200);
        builder.Property(o => o.PromotionDiscountAmount).HasColumnType("decimal(18,2)");

        builder.Property(o => o.Subtotal).HasColumnType("decimal(18,2)");
        builder.Property(o => o.Tax).HasColumnType("decimal(18,2)");
        builder.Property(o => o.GrandTotal).HasColumnType("decimal(18,2)");

        builder.Property(o => o.StockIssueMessage).HasMaxLength(500);
        builder.Property(o => o.AdminNotes).HasMaxLength(2000);

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.IdempotencyKey).IsUnique();
        builder.HasIndex(o => o.UserId);
        builder.HasIndex(o => o.Status);

        // Restrict on both - ShippingMethod/Promotion are soft-delete-only
        // (AuditableEntity), never physically removed, so an order referencing
        // one should never cascade. Same reasoning as Cart.AppliedPromotionId.
        builder.HasOne(o => o.ShippingMethod)
            .WithMany()
            .HasForeignKey(o => o.ShippingMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Promotion)
            .WithMany()
            .HasForeignKey(o => o.PromotionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Sku).HasMaxLength(100).IsRequired();
        builder.Property(i => i.VariantDescription).HasMaxLength(500);
        builder.Property(i => i.ImagePath).HasMaxLength(500);
        builder.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");

        builder.HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict (not Cascade) on Product/ProductVariant - same reasoning as
        // CartItemConfiguration: a product is never physically deleted in this
        // app (soft delete only), and having both a direct Product FK and an
        // indirect one via ProductVariant->Product rules out Cascade on both
        // anyway (SQL Server rejects multiple cascade paths to the same table).
        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ProductVariant)
            .WithMany()
            .HasForeignKey(i => i.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.OrderId);
    }
}
