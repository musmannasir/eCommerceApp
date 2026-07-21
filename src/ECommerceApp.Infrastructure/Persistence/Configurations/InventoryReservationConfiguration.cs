using ECommerceApp.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> builder)
    {
        builder.Property(r => r.ReferenceType).HasMaxLength(100);
        builder.Property(r => r.ReferenceId).HasMaxLength(100);

        builder.HasOne(r => r.InventoryItem)
            .WithMany()
            .HasForeignKey(r => r.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.InventoryItemId);
        builder.HasIndex(r => r.Status);
    }
}
