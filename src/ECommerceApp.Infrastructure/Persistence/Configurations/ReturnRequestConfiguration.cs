using ECommerceApp.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.Property(r => r.UserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.DecidedByUserId).HasMaxLength(450);
        builder.Property(r => r.Comment).HasMaxLength(1000);
        builder.Property(r => r.RejectionReason).HasMaxLength(1000);

        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReturnRequestItemConfiguration : IEntityTypeConfiguration<ReturnRequestItem>
{
    public void Configure(EntityTypeBuilder<ReturnRequestItem> builder)
    {
        builder.HasOne(i => i.ReturnRequest)
            .WithMany(r => r.Items)
            .HasForeignKey(i => i.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.OrderItem)
            .WithMany()
            .HasForeignKey(i => i.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
