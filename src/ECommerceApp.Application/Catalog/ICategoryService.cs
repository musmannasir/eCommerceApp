using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Catalog;

public interface ICategoryService
{
    Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> UpdateAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<CategoryDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CategoryTreeNodeDto>>> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CategoryDto>>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default);
}
