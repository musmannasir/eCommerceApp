using ECommerceApp.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.Property(s => s.Carrier).HasMaxLength(100).IsRequired();
        builder.Property(s => s.TrackingNumber).HasMaxLength(100).IsRequired();

        // At most one shipment per order (Milestone 10.3) - a v1 scope
        // choice, the same reasoning Payment's unique OrderId index uses.
        builder.HasIndex(s => s.OrderId).IsUnique();

        builder.HasOne(s => s.Order)
            .WithOne(o => o.Shipment)
            .HasForeignKey<Shipment>(s => s.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
