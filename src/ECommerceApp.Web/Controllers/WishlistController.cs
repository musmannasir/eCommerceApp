using System.Security.Claims;
using ECommerceApp.Application.Wishlist;
using ECommerceApp.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers;

public record ToggleWishlistRequest(int ProductId);

/// <summary>
/// Account-only (Milestone 6.3) - unlike Cart, wishlist has no guest cookie
/// concept, so every action here requires sign-in.
/// </summary>
[Authorize]
[Route("Wishlist")]
public class WishlistController : Controller
{
    private readonly IWishlistService _wishlistService;

    public WishlistController(IWishlistService wishlistService)
    {
        _wishlistService = wishlistService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var wishlist = await _wishlistService.GetWishlistAsync(UserId, cancellationToken);
        return View(wishlist);
    }

    [HttpPost("Toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle([FromBody] ToggleWishlistRequest request, CancellationToken cancellationToken)
    {
        var result = await _wishlistService.ToggleAsync(UserId, request.ProductId, cancellationToken);
        return result.IsFailure ? this.ToProblem(result.FirstError) : Json(result.Value);
    }

    [HttpPost("Remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove([FromBody] ToggleWishlistRequest request, CancellationToken cancellationToken)
    {
        var result = await _wishlistService.RemoveItemAsync(UserId, request.ProductId, cancellationToken);
        return Json(result);
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
