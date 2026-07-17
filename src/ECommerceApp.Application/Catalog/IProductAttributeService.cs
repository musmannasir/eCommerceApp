using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Catalog;

public interface IProductAttributeService
{
    Task<Result<ProductAttributeDto>> CreateAttributeAsync(CreateProductAttributeRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProductAttributeValueDto>> CreateValueAsync(CreateProductAttributeValueRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProductAttributeDto>>> GetAllAsync(CancellationToken cancellationToken = default);
}
