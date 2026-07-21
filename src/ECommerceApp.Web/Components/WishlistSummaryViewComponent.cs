using System.Security.Claims;
using ECommerceApp.Application.Wishlist;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Components;

/// <summary>
/// Renders the header's Wishlist link with an item-count badge. Wishlist is
/// account-only (Milestone 6.3), so an anonymous visitor just gets a badge-less
/// link to /Wishlist, which redirects to login via [Authorize].
/// </summary>
public class WishlistSummaryViewComponent : ViewComponent
{
    private readonly IWishlistService _wishlistService;

    public WishlistSummaryViewComponent(IWishlistService wishlistService)
    {
        _wishlistService = wishlistService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View(0);
        }

        var userId = ((ClaimsPrincipal)User).FindFirstValue(ClaimTypes.NameIdentifier)!;
        var wishlist = await _wishlistService.GetWishlistAsync(userId);
        return View(wishlist.Items.Count);
    }
}
