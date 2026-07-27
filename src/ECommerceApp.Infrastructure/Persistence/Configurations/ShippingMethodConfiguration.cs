using ECommerceApp.Domain.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class ShippingMethodConfiguration : IEntityTypeConfiguration<ShippingMethod>
{
    public void Configure(EntityTypeBuilder<ShippingMethod> builder)
    {
        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(500);
        builder.Property(m => m.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(m => m.RegionCode).HasMaxLength(10);
        builder.Property(m => m.BaseRate).HasColumnType("decimal(18,2)");
        builder.Property(m => m.RatePerKg).HasColumnType("decimal(18,2)");
        builder.Property(m => m.FreeShippingThreshold).HasColumnType("decimal(18,2)");

        // Unlike TaxRate (one rate per category per jurisdiction), several
        // named methods can coexist for the same jurisdiction - uniqueness
        // is on Name within the jurisdiction, not the jurisdiction alone.
        // Same dual-filtered-index technique as TaxRate/Carts for the
        // nullable RegionCode.
        builder.HasIndex(m => new { m.CountryCode, m.Name })
            .IsUnique()
            .HasFilter("[RegionCode] IS NULL");
        builder.HasIndex(m => new { m.CountryCode, m.RegionCode, m.Name })
            .IsUnique()
            .HasFilter("[RegionCode] IS NOT NULL");
    }
}
