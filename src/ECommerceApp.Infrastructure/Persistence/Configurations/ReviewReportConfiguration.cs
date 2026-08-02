using ECommerceApp.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class ReviewReportConfiguration : IEntityTypeConfiguration<ReviewReport>
{
    public void Configure(EntityTypeBuilder<ReviewReport> builder)
    {
        builder.Property(r => r.ReporterUserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(500);

        // One report per (review, reporter) - a repeat report from the same
        // customer doesn't add new signal, the same shape Review's own
        // one-per-(user,product) index uses.
        builder.HasIndex(r => new { r.ReviewId, r.ReporterUserId }).IsUnique();

        builder.HasOne(r => r.Review)
            .WithMany()
            .HasForeignKey(r => r.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
