using ECommerceApp.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class StoreSettingsConfiguration : IEntityTypeConfiguration<StoreSettings>
{
    public void Configure(EntityTypeBuilder<StoreSettings> builder)
    {
        builder.Property(s => s.StoreName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Currency).HasMaxLength(10).IsRequired();
        builder.Property(s => s.DefaultCountry).HasMaxLength(100).IsRequired();
        builder.Property(s => s.DefaultTaxCountryCode).HasMaxLength(2).IsRequired();
        builder.Property(s => s.DefaultTaxRegionCode).HasMaxLength(10);
        builder.Property(s => s.DefaultShippingCountryCode).HasMaxLength(2).IsRequired();
        builder.Property(s => s.DefaultShippingRegionCode).HasMaxLength(10);
    }
}
