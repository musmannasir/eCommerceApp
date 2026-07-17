using ECommerceApp.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> builder)
    {
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(a => a.Name).IsUnique();
    }
}

public class ProductAttributeValueConfiguration : IEntityTypeConfiguration<ProductAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProductAttributeValue> builder)
    {
        builder.Property(v => v.Value).HasMaxLength(100).IsRequired();
        builder.HasIndex(v => new { v.ProductAttributeId, v.Value }).IsUnique();

        builder.HasOne(v => v.ProductAttribute)
            .WithMany(a => a.Values)
            .HasForeignKey(v => v.ProductAttributeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
