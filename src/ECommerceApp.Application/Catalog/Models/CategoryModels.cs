namespace ECommerceApp.Application.Catalog.Models;

public record CategoryDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    int? ParentCategoryId,
    string? ParentCategoryName,
    int DisplayOrder,
    string? ImagePath,
    bool IsActive,
    bool IsFeatured,
    bool IsDeleted);

public record CategoryTreeNodeDto(
    int Id,
    string Name,
    string Slug,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<CategoryTreeNodeDto> Children);

public record CreateCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    int? ParentCategoryId,
    int DisplayOrder,
    bool IsActive,
    bool IsFeatured,
    string? ImagePath = null);

public record UpdateCategoryRequest(
    int Id,
    string Name,
    string? Slug,
    string? Description,
    int? ParentCategoryId,
    int DisplayOrder,
    bool IsActive,
    bool IsFeatured);
