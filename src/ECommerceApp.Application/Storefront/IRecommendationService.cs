using ECommerceApp.Application.Storefront.Models;

namespace ECommerceApp.Application.Storefront;

/// <summary>
/// "Recommendations v1" (Milestone 5.3) - scores candidate products by same
/// category, same brand, shared tags, and similar price range. "Best selling"
/// is explicitly not one of the signals yet: no Order/OrderItem history exists
/// until Milestone 9, and a signal that always contributes nothing would be
/// dead weight, not a real feature. The interface itself doesn't change when
/// that signal is added later - just the scoring inside the implementation -
/// which is the "keep the interface replaceable for a future engine" the
/// brief asks for.
/// </summary>
public interface IRecommendationService
{
    Task<IReadOnlyList<HomeProductCardDto>> GetRecommendationsAsync(int productId, int count, CancellationToken cancellationToken = default);
}
