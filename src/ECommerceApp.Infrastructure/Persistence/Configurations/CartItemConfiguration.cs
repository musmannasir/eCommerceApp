using ECommerceApp.Domain.Carts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.HasOne(ci => ci.Cart)
            .WithMany(c => c.Items)
            .HasForeignKey(ci => ci.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict (not Cascade) on Product/ProductVariant - same reasoning as
        // InventoryItemConfiguration: a product is never physically deleted in
        // this app (soft delete only), and having both a direct Product FK and
        // an indirect one via ProductVariant->Product rules out Cascade on both
        // anyway (SQL Server rejects multiple cascade paths to the same table).
        builder.HasOne(ci => ci.Product)
            .WithMany()
            .HasForeignKey(ci => ci.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ci => ci.ProductVariant)
            .WithMany()
            .HasForeignKey(ci => ci.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        // One line per product (no variant) or per variant - never both at once
        // for the same product, mirroring InventoryItemConfiguration's pair of
        // filtered unique indexes for the same "simple vs. variant" split.
        builder.HasIndex(ci => new { ci.CartId, ci.ProductId })
            .IsUnique()
            .HasFilter("[ProductVariantId] IS NULL");

        builder.HasIndex(ci => new { ci.CartId, ci.ProductVariantId })
            .IsUnique()
            .HasFilter("[ProductVariantId] IS NOT NULL");
    }
}
