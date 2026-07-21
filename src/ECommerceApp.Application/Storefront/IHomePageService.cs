using ECommerceApp.Application.Storefront.Models;

namespace ECommerceApp.Application.Storefront;

public interface IHomePageService
{
    Task<HomePageDto> GetHomePageAsync(CancellationToken cancellationToken = default);
}
