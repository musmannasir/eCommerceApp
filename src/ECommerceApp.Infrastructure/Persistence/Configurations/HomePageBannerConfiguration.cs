using ECommerceApp.Domain.Marketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class HomePageBannerConfiguration : IEntityTypeConfiguration<HomePageBanner>
{
    public void Configure(EntityTypeBuilder<HomePageBanner> builder)
    {
        builder.Property(b => b.Title).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Subtitle).HasMaxLength(500);
        builder.Property(b => b.ImagePath).HasMaxLength(500);
        builder.Property(b => b.LinkUrl).HasMaxLength(500);

        builder.HasIndex(b => new { b.BannerType, b.DisplayOrder });
    }
}
