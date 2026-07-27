using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Shipping;

public interface IShippingService
{
    Task<Result<ShippingMethodDto>> CreateAsync(CreateShippingMethodRequest request, CancellationToken cancellationToken = default);
    Task<Result<ShippingMethodDto>> UpdateAsync(UpdateShippingMethodRequest request, CancellationToken cancellationToken = default);
    Task<Result<ShippingMethodDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<ShippingMethodDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every active method available for a destination (an exact region
    /// match and a whole-country match both count - unlike TaxRate, a
    /// region-specific method doesn't suppress a country-wide one, since
    /// they're different named services, not competing rates for the same
    /// thing), each with its cost computed from <paramref name="totalWeightKg"/>
    /// and waived to zero once <paramref name="subtotal"/> meets the
    /// method's FreeShippingThreshold. Ordered by DisplayOrder. Destination-
    /// explicit and ready for Milestone 8.2's checkout method picker.
    /// </summary>
    Task<IReadOnlyList<ShippingOptionDto>> GetAvailableShippingOptionsAsync(
        decimal totalWeightKg, decimal subtotal, string countryCode, string? regionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience wrapper for Cart's "estimated shipping" display
    /// (Milestone 7.3): the cheapest available option against the store's
    /// configured default jurisdiction (Store:DefaultShippingCountryCode/
    /// RegionCode) rather than a real customer destination - there's no
    /// Address entity to derive one from until Milestone 8.1, and no
    /// method-picker UI to show multiple options until Milestone 8.2.
    /// </summary>
    Task<EstimatedShippingResult> CalculateEstimatedShippingAsync(
        decimal totalWeightKg, decimal subtotal, CancellationToken cancellationToken = default);
}
