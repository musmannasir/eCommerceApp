using ECommerceApp.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Slug).HasMaxLength(250).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(2000);
        builder.Property(b => b.LogoPath).HasMaxLength(500);
        builder.Property(b => b.Website).HasMaxLength(300);

        builder.HasIndex(b => b.Slug).IsUnique();
    }
}
