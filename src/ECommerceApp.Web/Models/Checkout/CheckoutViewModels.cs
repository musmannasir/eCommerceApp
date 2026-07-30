using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Application.Shipping.Models;

namespace ECommerceApp.Web.Models.Checkout;

/// <summary>Step 1 - address selection (Milestone 8.2).</summary>
public record CheckoutAddressPageViewModel(
    IReadOnlyList<AddressDto> Addresses, int SelectedAddressId, int ItemCount, decimal Subtotal);

/// <summary>
/// Step 2 - shipping method selection. Options are already computed for
/// AddressId's jurisdiction against the post-discount subtotal (Milestone
/// 7.4's fix applied for real here, not just as an estimate). Options empty
/// means no shipping method is configured for this address at all.
/// </summary>
public record CheckoutShippingPageViewModel(
    int AddressId, AddressDto Address, IReadOnlyList<ShippingOptionDto> Options, int? SelectedShippingMethodId, int ItemCount, decimal Subtotal);

/// <summary>
/// Step 3 - final review. Calculation is the real, destination-based total
/// (ICheckoutCalculationService.CalculateAsync), not the Cart page's
/// store-default-jurisdiction estimate. IdempotencyKey (Milestone 8.3) is a
/// fresh single-use token generated on every GET, submitted alongside
/// PlaceOrder so a double-click/back-button resubmit replays the same
/// validated outcome instead of re-running (and possibly re-failing) the
/// checks.
/// </summary>
public record CheckoutReviewPageViewModel(
    AddressDto Address, ShippingOptionDto ShippingOption, IReadOnlyList<CartItemDto> Items, CheckoutCalculationResult Calculation, string IdempotencyKey);

/// <summary>
/// Milestone 9.1 - the real, persisted Order created by a successful
/// PlaceOrder submission (previously, before Order entities existed,
/// Milestone 8.3 rendered a frozen-but-unpersisted DTO from a cache here).
/// </summary>
public record CheckoutConfirmationPageViewModel(OrderDto Order);
