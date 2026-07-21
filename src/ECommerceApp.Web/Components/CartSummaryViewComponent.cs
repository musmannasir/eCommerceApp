using ECommerceApp.Application.Carts;
using ECommerceApp.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Components;

/// <summary>
/// Renders the header's Cart link with an item-count badge. Uses
/// TryGetOwner() (not GetOrCreateOwner()) - this renders on every single page,
/// so a visitor who has never added anything must not be handed a guest-cart
/// cookie just for looking around.
/// </summary>
public class CartSummaryViewComponent : ViewComponent
{
    private readonly ICartService _cartService;
    private readonly ICartOwnerAccessor _cartOwnerAccessor;

    public CartSummaryViewComponent(ICartService cartService, ICartOwnerAccessor cartOwnerAccessor)
    {
        _cartService = cartService;
        _cartOwnerAccessor = cartOwnerAccessor;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var owner = _cartOwnerAccessor.TryGetOwner();
        var itemCount = owner is null ? 0 : (await _cartService.GetCartAsync(owner)).TotalItemCount;
        return View(itemCount);
    }
}
