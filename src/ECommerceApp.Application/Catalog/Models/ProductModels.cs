namespace ECommerceApp.Application.Catalog.Models;

public record ProductImageDto(int Id, int? ProductVariantId, string Path, string? AltText, int DisplayOrder, bool IsPrimary);

public record ProductVariantAttributeValueDto(int ProductAttributeId, string ProductAttributeName, int ProductAttributeValueId, string Value);

public record ProductVariantDto(
    int Id,
    string SKU,
    string? Barcode,
    decimal? CostPrice,
    decimal? SellingPrice,
    decimal? CompareAtPrice,
    decimal? Weight,
    bool IsActive,
    IReadOnlyList<ProductVariantAttributeValueDto> AttributeValues);

public record ProductSpecificationDto(int Id, string Name, string Value, int DisplayOrder);

public record ProductTagRefDto(int Id, string Name);

public record ProductListItemDto(
    int Id,
    string Name,
    string Slug,
    string BaseSKU,
    string? BrandName,
    string CategoryName,
    decimal SellingPrice,
    bool IsActive,
    bool IsPublished,
    bool IsFeatured,
    bool IsDeleted);

public record ProductDto(
    int Id,
    string Name,
    string Slug,
    string? ShortDescription,
    string? FullDescription,
    int? BrandId,
    string? BrandName,
    int CategoryId,
    string CategoryName,
    string BaseSKU,
    decimal CostPrice,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    string TaxCategory,
    bool IsTaxable,
    bool IsActive,
    bool IsFeatured,
    bool IsPublished,
    DateTime? PublishedAtUtc,
    decimal? Weight,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    string? WarrantyInformation,
    string? ReturnEligibility,
    int? LowStockThreshold,
    string? SearchKeywords,
    string? MetaTitle,
    string? MetaDescription,
    bool IsDeleted,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductSpecificationDto> Specifications,
    IReadOnlyList<ProductTagRefDto> Tags);

public record ProductListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public int? BrandId { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    /// <summary>When true, lists only soft-deleted rows (for a "Recycle bin"/restore view) instead of active ones.</summary>
    public bool OnlyDeleted { get; init; }
}

public record CreateProductRequest(
    string Name,
    string? Slug,
    string? ShortDescription,
    string? FullDescription,
    int? BrandId,
    int CategoryId,
    string BaseSKU,
    decimal CostPrice,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    string TaxCategory,
    bool IsTaxable,
    bool IsActive,
    bool IsFeatured,
    decimal? Weight,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    string? WarrantyInformation,
    string? ReturnEligibility,
    int? LowStockThreshold,
    string? SearchKeywords,
    string? MetaTitle,
    string? MetaDescription);

public record UpdateProductRequest(
    int Id,
    string Name,
    string? Slug,
    string? ShortDescription,
    string? FullDescription,
    int? BrandId,
    int CategoryId,
    string BaseSKU,
    decimal CostPrice,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    string TaxCategory,
    bool IsTaxable,
    bool IsActive,
    bool IsFeatured,
    decimal? Weight,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    string? WarrantyInformation,
    string? ReturnEligibility,
    int? LowStockThreshold,
    string? SearchKeywords,
    string? MetaTitle,
    string? MetaDescription);

public record CreateVariantRequest(
    int ProductId,
    string SKU,
    string? Barcode,
    decimal? CostPrice,
    decimal? SellingPrice,
    decimal? CompareAtPrice,
    decimal? Weight,
    bool IsActive,
    IReadOnlyList<int> AttributeValueIds);

public record CreateSpecificationRequest(int ProductId, string Name, string Value, int DisplayOrder);
