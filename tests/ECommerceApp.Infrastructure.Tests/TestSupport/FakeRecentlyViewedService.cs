using ECommerceApp.Application.Storefront;
using ECommerceApp.Application.Storefront.Models;

namespace ECommerceApp.Infrastructure.Tests.TestSupport;

/// <summary>
/// The real IRecentlyViewedService implementation lives in the Web project
/// (it needs HttpContext for the guest cookie), which Infrastructure.Tests
/// doesn't reference - so callers that only need to satisfy
/// HomePageService/ProductDetailService's constructor use this in-memory
/// double instead. RecordViewAsync tracks calls so a test can assert on them.
/// </summary>
public class FakeRecentlyViewedService : IRecentlyViewedService
{
    public List<int> RecordedProductIds { get; } = new();
    public List<HomeProductCardDto> ItemsToReturn { get; set; } = new();

    public Task RecordViewAsync(int productId, CancellationToken cancellationToken = default)
    {
        RecordedProductIds.Add(productId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HomeProductCardDto>> GetRecentlyViewedAsync(int? excludeProductId = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HomeProductCardDto> result = excludeProductId.HasValue
            ? ItemsToReturn.Where(i => i.Id != excludeProductId.Value).ToList()
            : ItemsToReturn;
        return Task.FromResult(result);
    }
}
