using ECommerceApp.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.Property(v => v.SKU).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Barcode).HasMaxLength(100);
        builder.Property(v => v.CombinationKey).HasMaxLength(200).IsRequired();

        builder.Property(v => v.CostPrice).HasColumnType("decimal(18,2)");
        builder.Property(v => v.SellingPrice).HasColumnType("decimal(18,2)");
        builder.Property(v => v.CompareAtPrice).HasColumnType("decimal(18,2)");
        builder.Property(v => v.Weight).HasColumnType("decimal(18,3)");

        builder.HasIndex(v => v.SKU).IsUnique();
        builder.HasIndex(v => new { v.ProductId, v.CombinationKey }).IsUnique();
    }
}

public class ProductVariantAttributeValueConfiguration : IEntityTypeConfiguration<ProductVariantAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProductVariantAttributeValue> builder)
    {
        builder.HasIndex(v => new { v.ProductVariantId, v.ProductAttributeValueId }).IsUnique();

        builder.HasOne(v => v.ProductVariant)
            .WithMany(pv => pv.AttributeValues)
            .HasForeignKey(v => v.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict rather than cascade: an attribute VALUE being removed shouldn't silently
        // delete the variants built from it - the app must reassign/remove those explicitly.
        builder.HasOne(v => v.ProductAttributeValue)
            .WithMany()
            .HasForeignKey(v => v.ProductAttributeValueId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
