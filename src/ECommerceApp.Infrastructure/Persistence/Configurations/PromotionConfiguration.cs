using ECommerceApp.Domain.Marketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.CouponCode).HasMaxLength(50);
        builder.Property(p => p.DiscountValue).HasColumnType("decimal(18,2)");
        builder.Property(p => p.MinimumOrderAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.MaxDiscountAmount).HasColumnType("decimal(18,2)");

        // A code-based promotion's code must be unique; an automatic promotion
        // (CouponCode null) never collides with anything, so the index is
        // filtered the same way Carts' UserId/GuestToken indexes are.
        builder.HasIndex(p => p.CouponCode).IsUnique().HasFilter("[CouponCode] IS NOT NULL");

        // Restrict, not Cascade - same reasoning as every other FK into
        // Categories/Brands/Products in this app: those are never physically
        // deleted (soft delete only), so a promotion scoped to one should
        // never silently vanish either.
        builder.HasOne(p => p.ScopeCategory)
            .WithMany()
            .HasForeignKey(p => p.ScopeCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ScopeBrand)
            .WithMany()
            .HasForeignKey(p => p.ScopeBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ScopeProduct)
            .WithMany()
            .HasForeignKey(p => p.ScopeProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
