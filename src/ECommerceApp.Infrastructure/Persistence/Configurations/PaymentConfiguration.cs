using ECommerceApp.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.MaskedCardNumber).HasMaxLength(32).IsRequired();
        builder.Property(p => p.CardBrand).HasMaxLength(50).IsRequired();
        builder.Property(p => p.DeclineReason).HasMaxLength(200);

        // At most one payment attempt per order - Milestone 9.2 charges the
        // card once, synchronously, as part of creating the order; a
        // declined card does not retry in place (see Order's own remarks).
        builder.HasIndex(p => p.OrderId).IsUnique();

        builder.HasOne(p => p.Order)
            .WithOne(o => o.Payment)
            .HasForeignKey<Payment>(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
