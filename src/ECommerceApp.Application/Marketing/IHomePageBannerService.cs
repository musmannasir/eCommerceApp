using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Marketing;

public interface IHomePageBannerService
{
    Task<Result<HomePageBannerDto>> CreateAsync(CreateHomePageBannerRequest request, CancellationToken cancellationToken = default);
    Task<Result<HomePageBannerDto>> UpdateAsync(UpdateHomePageBannerRequest request, CancellationToken cancellationToken = default);
    Task<Result<HomePageBannerDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<HomePageBannerDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<Result> SetImageAsync(int id, string imagePath, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default);
}
