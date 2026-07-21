using ECommerceApp.Application.Wishlist.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Wishlist;

/// <summary>
/// Wishlist (Milestone 6.3) - account-only, no guest support (unlike Cart),
/// since it's meant to persist indefinitely and follow the customer across
/// devices, which a cookie can't do. Product-level only, no variant - a
/// lighter bookmark than a cart line.
/// </summary>
public interface IWishlistService
{
    Task<WishlistDto> GetWishlistAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Adds the product if not already wishlisted, removes it if it is.</summary>
    Task<Result<WishlistToggleResultDto>> ToggleAsync(string userId, int productId, CancellationToken cancellationToken = default);

    Task<WishlistToggleResultDto> RemoveItemAsync(string userId, int productId, CancellationToken cancellationToken = default);

    Task<bool> IsWishlistedAsync(string userId, int productId, CancellationToken cancellationToken = default);
}
