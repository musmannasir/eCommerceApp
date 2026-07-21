using ECommerceApp.Domain.Wishlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.Property(w => w.UserId).HasMaxLength(450).IsRequired();

        // One row per (user, product) - toggling an already-wishlisted product
        // removes the row rather than allowing a duplicate.
        builder.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();

        builder.HasOne(w => w.Product)
            .WithMany()
            .HasForeignKey(w => w.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
