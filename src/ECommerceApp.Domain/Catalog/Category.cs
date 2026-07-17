using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Catalog;

/// <summary>
/// Supports unlimited nesting at the database level via <see cref="ParentCategoryId"/>;
/// the Admin UI applies a shallower display depth, and application logic prevents a
/// category from becoming its own ancestor.
/// </summary>
public class Category : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentCategoryId { get; set; }
    public int DisplayOrder { get; set; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }

    public Category? ParentCategory { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
}
