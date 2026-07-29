using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Carts;

/// <summary>
/// Cart core (Milestone 6.1) plus merge and pricing/stock integrity
/// (Milestone 6.2). Every mutating method returns the full, freshly rebuilt
/// CartDto on success, so the caller (an AJAX endpoint) can re-render the
/// cart summary in one round trip without a second read.
/// </summary>
public interface ICartService
{
    Task<CartDto> GetCartAsync(CartOwner owner, CancellationToken cancellationToken = default);

    Task<Result<CartDto>> AddItemAsync(CartOwner owner, AddCartItemRequest request, CancellationToken cancellationToken = default);

    Task<Result<CartDto>> UpdateQuantityAsync(CartOwner owner, UpdateCartItemQuantityRequest request, CancellationToken cancellationToken = default);

    Task<Result<CartDto>> RemoveItemAsync(CartOwner owner, int cartItemId, CancellationToken cancellationToken = default);

    Task<CartDto> ClearCartAsync(CartOwner owner, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges a guest cart into a user's cart right after sign-in/registration.
    /// If the user has no cart yet, the guest cart is simply reassigned to
    /// them. If both exist, each guest line either increments a matching
    /// user-cart line (re-validated against current stock, capping rather
    /// than failing outright since there's no request to reject here) or
    /// moves over as a new line; the now-empty guest cart is deleted either
    /// way. A no-op (returns the user's existing cart, or an empty one) if
    /// there's no guest cart to merge.
    /// </summary>
    Task<CartDto> MergeGuestCartIntoUserCartAsync(string guestToken, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the coupon against the cart's current contents (via
    /// IPromotionService) and, on success, sets it as the cart's one applied
    /// promotion - Milestone 7.1's v1 rule is at most one at a time, so this
    /// replaces whatever was applied before rather than stacking.
    /// </summary>
    Task<Result<CartDto>> ApplyCouponAsync(CartOwner owner, string couponCode, CancellationToken cancellationToken = default);

    Task<CartDto> RemoveCouponAsync(CartOwner owner, CancellationToken cancellationToken = default);

    /// <summary>
    /// The cart's currently-available lines plus the resolved applied
    /// promotion id, in the shape ICheckoutCalculationService.CalculateAsync
    /// needs (Milestone 8.2) - Failure ("cart.empty") if there's nothing
    /// available to check out.
    /// </summary>
    Task<Result<CheckoutInputDto>> GetCheckoutInputAsync(CartOwner owner, CancellationToken cancellationToken = default);
}
