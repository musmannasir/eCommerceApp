using ECommerceApp.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.Property(w => w.Name).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Code).HasMaxLength(50).IsRequired();
        builder.Property(w => w.AddressLine1).HasMaxLength(200);
        builder.Property(w => w.AddressLine2).HasMaxLength(200);
        builder.Property(w => w.City).HasMaxLength(100);
        builder.Property(w => w.Region).HasMaxLength(100);
        builder.Property(w => w.PostalCode).HasMaxLength(20);
        builder.Property(w => w.Country).HasMaxLength(100);

        builder.HasIndex(w => w.Code).IsUnique();
    }
}
