using ECommerceApp.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.Property(r => r.Amount).HasColumnType("decimal(18,2)");
        builder.Property(r => r.ProcessedByUserId).HasMaxLength(450);

        // At most one refund per return request - ReturnRequestStatus.Refunded
        // is the only route to creating one, so the service-layer status
        // check already prevents a second attempt; this is defense in depth,
        // the same relationship Payment.OrderId's own unique index has.
        builder.HasIndex(r => r.ReturnRequestId).IsUnique();

        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReturnRequest)
            .WithMany()
            .HasForeignKey(r => r.ReturnRequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
