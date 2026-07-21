using ECommerceApp.Domain.Storefront;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class RecentlyViewedItemConfiguration : IEntityTypeConfiguration<RecentlyViewedItem>
{
    public void Configure(EntityTypeBuilder<RecentlyViewedItem> builder)
    {
        builder.Property(r => r.UserId).HasMaxLength(450).IsRequired();

        // One row per (user, product) - viewing again updates ViewedAtUtc in place
        // instead of inserting a duplicate.
        builder.HasIndex(r => new { r.UserId, r.ProductId }).IsUnique();
        builder.HasIndex(r => new { r.UserId, r.ViewedAtUtc });

        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
