using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Catalog;

public interface IBrandService
{
    Task<Result<BrandDto>> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> UpdateAsync(UpdateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<BrandDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BrandDto>>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Result> SetLogoAsync(int id, string logoPath, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default);
}
