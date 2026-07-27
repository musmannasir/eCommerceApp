using ECommerceApp.Domain.Taxation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.Property(r => r.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(r => r.RegionCode).HasMaxLength(10);
        builder.Property(r => r.TaxCategory).HasMaxLength(50).IsRequired();
        // Real-world sales tax rates carry more precision than money amounts
        // (e.g. US combined state+local rates like 7.375%), hence 4 decimal
        // places instead of the usual decimal(18,2) money convention.
        builder.Property(r => r.RatePercent).HasColumnType("decimal(9,4)");

        // Two filtered unique indexes, same technique as Carts' UserId/
        // GuestToken pair - SQL Server's plain composite unique index would
        // treat every row's NULL RegionCode as distinct, which would let the
        // same CountryCode+TaxCategory repeat indefinitely with RegionCode
        // left NULL each time (only one country-wide rate per category
        // should ever exist).
        builder.HasIndex(r => new { r.CountryCode, r.TaxCategory })
            .IsUnique()
            .HasFilter("[RegionCode] IS NULL");
        builder.HasIndex(r => new { r.CountryCode, r.RegionCode, r.TaxCategory })
            .IsUnique()
            .HasFilter("[RegionCode] IS NOT NULL");
    }
}
