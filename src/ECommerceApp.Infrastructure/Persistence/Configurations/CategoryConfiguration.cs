using ECommerceApp.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(250).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.ImagePath).HasMaxLength(500);

        builder.HasIndex(c => c.Slug).IsUnique();

        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
