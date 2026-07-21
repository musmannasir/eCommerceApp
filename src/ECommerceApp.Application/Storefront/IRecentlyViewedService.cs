using ECommerceApp.Application.Storefront.Models;

namespace ECommerceApp.Application.Storefront;

/// <summary>
/// Guests are tracked via a secure, non-sensitive cookie (product IDs only,
/// nothing personal); authenticated customers are tracked in the database so
/// the history follows them across devices. Milestone 5.3.
/// </summary>
public interface IRecentlyViewedService
{
    /// <summary>Records that the current visitor (guest or authenticated) viewed this
    /// product - moves it to the front of their history and trims to the configured max.</summary>
    Task RecordViewAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Most-recently-viewed first. excludeProductId keeps a product's own detail
    /// page from listing itself (it was just recorded as "viewed" by this very request).</summary>
    Task<IReadOnlyList<HomeProductCardDto>> GetRecentlyViewedAsync(int? excludeProductId = null, CancellationToken cancellationToken = default);
}
