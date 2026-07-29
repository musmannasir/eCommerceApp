using ECommerceApp.Application.Checkout.Models;

namespace ECommerceApp.Application.Checkout;

/// <summary>
/// Combines Promotion + Tax + Shipping into one final total (Milestone
/// 7.4) - the orchestrator IPricingService/IPromotionService/ITaxService/
/// IShippingService were each already built to feed into. Never fails -
/// an invalid/missing promotion is simply treated as "no discount" (the
/// caller, e.g. CartService, is responsible for actually clearing an
/// invalid one from persisted state; this is a pure calculator with no
/// side effects, same as IPricingService).
/// </summary>
public interface ICheckoutCalculationService
{
    /// <summary>
    /// Full calculation against an explicit destination - ready for
    /// Milestone 8.2's checkout once a real Address exists (Milestone
    /// 8.1). <paramref name="selectedShippingMethodId"/> defaults to the
    /// cheapest available option when null, since there's no method-picker
    /// UI yet.
    /// </summary>
    Task<CheckoutCalculationResult> CalculateAsync(
        IReadOnlyList<CheckoutLineDto> lines,
        int? appliedPromotionId,
        string taxCountryCode,
        string? taxRegionCode,
        string shippingCountryCode,
        string? shippingRegionCode,
        int? selectedShippingMethodId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Store-default-jurisdiction convenience wrapper - what the Cart
    /// page's "Estimated tax"/"Estimated shipping"/"Estimated total" lines
    /// use today, now correctly computed against the post-discount amount
    /// (the gap Milestones 7.2/7.3 each explicitly deferred to this
    /// service).
    /// </summary>
    Task<CheckoutCalculationResult> CalculateEstimatedAsync(
        IReadOnlyList<CheckoutLineDto> lines, int? appliedPromotionId, CancellationToken cancellationToken = default);
}
