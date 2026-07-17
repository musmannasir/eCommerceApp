using ECommerceApp.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.Property(i => i.Path).HasMaxLength(500).IsRequired();
        builder.Property(i => i.AltText).HasMaxLength(200);

        // Product already cascades to ProductImage directly (ProductConfiguration). SQL Server
        // treats SET NULL the same as CASCADE for its "multiple cascade paths" check, so even
        // SetNull here would conflict with that path - NoAction avoids any DB-level action, and
        // ProductService.DeleteVariantAsync detaches the variant's images in application code
        // before removing it.
        builder.HasOne(i => i.ProductVariant)
            .WithMany()
            .HasForeignKey(i => i.ProductVariantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
