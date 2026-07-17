using ECommerceApp.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class ProductTagConfiguration : IEntityTypeConfiguration<ProductTag>
{
    public void Configure(EntityTypeBuilder<ProductTag> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(120).IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique();
    }
}

public class ProductTagMappingConfiguration : IEntityTypeConfiguration<ProductTagMapping>
{
    public void Configure(EntityTypeBuilder<ProductTagMapping> builder)
    {
        builder.HasIndex(m => new { m.ProductId, m.ProductTagId }).IsUnique();

        builder.HasOne(m => m.ProductTag)
            .WithMany(t => t.ProductMappings)
            .HasForeignKey(m => m.ProductTagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
