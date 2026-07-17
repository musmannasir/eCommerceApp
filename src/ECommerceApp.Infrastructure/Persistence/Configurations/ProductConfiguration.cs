using ECommerceApp.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(350).IsRequired();
        builder.Property(p => p.ShortDescription).HasMaxLength(500);
        builder.Property(p => p.BaseSKU).HasMaxLength(100).IsRequired();
        builder.Property(p => p.TaxCategory).HasMaxLength(50);
        builder.Property(p => p.WarrantyInformation).HasMaxLength(1000);
        builder.Property(p => p.ReturnEligibility).HasMaxLength(500);
        builder.Property(p => p.SearchKeywords).HasMaxLength(500);
        builder.Property(p => p.MetaTitle).HasMaxLength(200);
        builder.Property(p => p.MetaDescription).HasMaxLength(500);

        builder.Property(p => p.CostPrice).HasColumnType("decimal(18,2)");
        builder.Property(p => p.SellingPrice).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CompareAtPrice).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Weight).HasColumnType("decimal(18,3)");
        builder.Property(p => p.Length).HasColumnType("decimal(18,3)");
        builder.Property(p => p.Width).HasColumnType("decimal(18,3)");
        builder.Property(p => p.Height).HasColumnType("decimal(18,3)");

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.BaseSKU).IsUnique();

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Brand)
            .WithMany()
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Specifications)
            .WithOne(s => s.Product)
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.TagMappings)
            .WithOne(m => m.Product)
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
