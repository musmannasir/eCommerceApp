namespace ECommerceApp.Application.Catalog.Models;

public record BrandDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string? LogoPath,
    string? Website,
    bool IsActive,
    bool IsFeatured,
    bool IsDeleted);

public record CreateBrandRequest(
    string Name,
    string? Slug,
    string? Description,
    string? Website,
    bool IsActive,
    bool IsFeatured);

public record UpdateBrandRequest(
    int Id,
    string Name,
    string? Slug,
    string? Description,
    string? Website,
    bool IsActive,
    bool IsFeatured);
