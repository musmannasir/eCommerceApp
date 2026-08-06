using ECommerceApp.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(m => m.PayloadJson).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);

        // The processor's own lookup - every Pending row, oldest first.
        builder.HasIndex(m => m.Status);
    }
}
