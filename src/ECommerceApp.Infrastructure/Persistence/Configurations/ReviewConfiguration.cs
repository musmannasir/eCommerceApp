using ECommerceApp.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.Property(r => r.UserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.Title).HasMaxLength(150);
        builder.Property(r => r.Body).HasMaxLength(2000).IsRequired();

        // One review per (user, product) - toggling isn't a concept here, but
        // a resubmission is rejected as a conflict rather than allowed to
        // duplicate, the same shape WishlistItem's own unique index uses.
        builder.HasIndex(r => new { r.UserId, r.ProductId }).IsUnique();

        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
