using ECommerceApp.Domain.Carts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.Property(c => c.UserId).HasMaxLength(450);
        builder.Property(c => c.GuestToken).HasMaxLength(64);

        // One cart per authenticated user, one per guest token - never more than
        // one row for the same owner, but both columns are nullable so a regular
        // unique index (which lets multiple NULLs through) would not enforce it.
        builder.HasIndex(c => c.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        builder.HasIndex(c => c.GuestToken).IsUnique().HasFilter("[GuestToken] IS NOT NULL");

        // Restrict - a promotion is soft-delete-only (AuditableEntity), never
        // physically removed, so a cart referencing one should never cascade.
        builder.HasOne(c => c.AppliedPromotion)
            .WithMany()
            .HasForeignKey(c => c.AppliedPromotionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
