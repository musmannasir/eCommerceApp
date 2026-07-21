using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Catalog;

public interface IProductService
{
    Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> UpdateAsync(UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<ProductListItemDto>>> GetPagedAsync(ProductListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Lightweight active-product-plus-variant list for picker UIs (e.g. recording opening stock) - avoids loading each product's full detail graph.</summary>
    Task<Result<IReadOnlyList<ProductPickerItemDto>>> GetPickerListAsync(CancellationToken cancellationToken = default);

    Task<Result> PublishAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> UnpublishAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<ProductVariantDto>> AddVariantAsync(CreateVariantRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteVariantAsync(int variantId, CancellationToken cancellationToken = default);

    Task<Result<ProductSpecificationDto>> AddSpecificationAsync(CreateSpecificationRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteSpecificationAsync(int specificationId, CancellationToken cancellationToken = default);

    Task<Result> AddTagAsync(int productId, string tagName, CancellationToken cancellationToken = default);
    Task<Result> RemoveTagAsync(int productId, int productTagId, CancellationToken cancellationToken = default);

    Task<Result<ProductImageDto>> AddImageAsync(
        int productId,
        int? productVariantId,
        Stream content,
        string fileName,
        string contentType,
        string? altText,
        bool isPrimary,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteImageAsync(int imageId, CancellationToken cancellationToken = default);
}
