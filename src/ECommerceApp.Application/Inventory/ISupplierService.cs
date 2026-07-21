using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Inventory;

public interface ISupplierService
{
    Task<Result<SupplierDto>> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<Result<SupplierDto>> UpdateAsync(UpdateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<Result<SupplierDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<SupplierDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SupplierDto>>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SupplierProductDto>>> GetLinkedProductsAsync(int supplierId, CancellationToken cancellationToken = default);
    Task<Result<SupplierProductDto>> LinkProductAsync(LinkSupplierProductRequest request, CancellationToken cancellationToken = default);
    Task<Result<SupplierProductDto>> UpdateProductLinkAsync(UpdateSupplierProductRequest request, CancellationToken cancellationToken = default);
    Task<Result> UnlinkProductAsync(int supplierProductId, CancellationToken cancellationToken = default);
}
