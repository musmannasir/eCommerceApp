using ECommerceApp.Application.Carts;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Web.Extensions;
using ECommerceApp.Web.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers;

public record RemoveCartItemRequest(int CartItemId);

/// <summary>
/// The cart page itself is a normal GET view; every mutation is a small AJAX
/// JSON endpoint (like the M5.2 live variant resolver), CSRF-protected via a
/// request header instead of a form field, since there's no posted &lt;form&gt;
/// - see _Layout.cshtml's csrf-token meta tag and site.js's fetch wrapper.
/// </summary>
[Route("Cart")]
public class CartController : Controller
{
    private readonly ICartService _cartService;
    private readonly ICartOwnerAccessor _cartOwnerAccessor;
    private readonly IValidator<AddCartItemRequest> _addValidator;
    private readonly IValidator<UpdateCartItemQuantityRequest> _updateValidator;

    public CartController(
        ICartService cartService,
        ICartOwnerAccessor cartOwnerAccessor,
        IValidator<AddCartItemRequest> addValidator,
        IValidator<UpdateCartItemQuantityRequest> updateValidator)
    {
        _cartService = cartService;
        _cartOwnerAccessor = cartOwnerAccessor;
        _addValidator = addValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owner = _cartOwnerAccessor.TryGetOwner();
        var cart = owner is null ? EmptyCart : await _cartService.GetCartAsync(owner, cancellationToken);
        return View(cart);
    }

    [HttpGet("Summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var owner = _cartOwnerAccessor.TryGetOwner();
        var cart = owner is null ? EmptyCart : await _cartService.GetCartAsync(owner, cancellationToken);
        return Json(new { itemCount = cart.TotalItemCount });
    }

    [HttpPost("Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add([FromBody] AddCartItemRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateOrNullAsync(_addValidator, request, cancellationToken) is { } validationProblem)
        {
            return validationProblem;
        }

        var owner = _cartOwnerAccessor.GetOrCreateOwner();
        var result = await _cartService.AddItemAsync(owner, request, cancellationToken);
        return result.IsFailure ? this.ToProblem(result.FirstError) : Json(result.Value);
    }

    [HttpPost("UpdateQuantity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity([FromBody] UpdateCartItemQuantityRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateOrNullAsync(_updateValidator, request, cancellationToken) is { } validationProblem)
        {
            return validationProblem;
        }

        var owner = _cartOwnerAccessor.TryGetOwner();
        if (owner is null)
        {
            return this.ToProblem(Error.NotFound("cart.item_not_found", "This item is no longer in your cart."));
        }

        var result = await _cartService.UpdateQuantityAsync(owner, request, cancellationToken);
        return result.IsFailure ? this.ToProblem(result.FirstError) : Json(result.Value);
    }

    [HttpPost("Remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove([FromBody] RemoveCartItemRequest request, CancellationToken cancellationToken)
    {
        var owner = _cartOwnerAccessor.TryGetOwner();
        if (owner is null)
        {
            return this.ToProblem(Error.NotFound("cart.item_not_found", "This item is no longer in your cart."));
        }

        var result = await _cartService.RemoveItemAsync(owner, request.CartItemId, cancellationToken);
        return result.IsFailure ? this.ToProblem(result.FirstError) : Json(result.Value);
    }

    [HttpPost("Clear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var owner = _cartOwnerAccessor.TryGetOwner();
        var cart = owner is null ? EmptyCart : await _cartService.ClearCartAsync(owner, cancellationToken);
        return Json(cart);
    }

    private static CartDto EmptyCart => new(null, Array.Empty<CartItemDto>(), 0, 0);
}
