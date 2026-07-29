using System.Security.Claims;
using ECommerceApp.Application.Addresses;
using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Carts;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Shipping;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Web.Models.Checkout;
using ECommerceApp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerceApp.Web.Controllers;

/// <summary>
/// Checkout flow UI (Milestone 8.2) - a stateless, three-step review flow
/// (address -> shipping method -> final review) carried entirely via query
/// string, no session/wizard state needed. Account-only ([Authorize]) since
/// Address (Milestone 8.1) has no guest concept - a guest with items in
/// their cart is redirected to log in like any other [Authorize] page, and
/// their guest cart already merges into their account on login (Milestone
/// 6.2), so nothing is lost.
///
/// Milestone 8.3 adds server-side revalidation and idempotency to the final
/// submission. Order placement itself is still out of scope - Order
/// entities don't exist until Milestone 9.1 - so a successful PlaceOrder
/// lands on a Confirmation page explicit that this is a validated, not yet
/// persisted, outcome.
/// </summary>
[Authorize]
[Route("Checkout")]
public class CheckoutController : Controller
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromMinutes(15);

    private readonly ICartService _cartService;
    private readonly IAddressService _addressService;
    private readonly IShippingService _shippingService;
    private readonly ICheckoutCalculationService _checkoutCalculationService;
    private readonly ICartOwnerAccessor _cartOwnerAccessor;
    private readonly IMemoryCache _cache;

    public CheckoutController(
        ICartService cartService, IAddressService addressService, IShippingService shippingService,
        ICheckoutCalculationService checkoutCalculationService, ICartOwnerAccessor cartOwnerAccessor, IMemoryCache cache)
    {
        _cartService = cartService;
        _addressService = addressService;
        _shippingService = shippingService;
        _checkoutCalculationService = checkoutCalculationService;
        _cartOwnerAccessor = cartOwnerAccessor;
        _cache = cache;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var checkoutInput = await _cartService.GetCheckoutInputAsync(Owner, cancellationToken);
        if (checkoutInput.IsFailure)
        {
            TempData["Error"] = checkoutInput.FirstError.Message;
            return RedirectToAction(nameof(CartController.Index), "Cart");
        }

        var cart = await _cartService.GetCartAsync(Owner, cancellationToken);
        if (HasStockIssues(cart))
        {
            TempData["Error"] = StockIssuesMessage(cart);
            return RedirectToAction(nameof(CartController.Index), "Cart");
        }

        var addresses = await _addressService.GetAddressesAsync(UserId, cancellationToken);
        if (addresses.Count == 0)
        {
            TempData["Message"] = "Add a shipping address to continue to checkout.";
            return RedirectToAction(
                nameof(AddressesController.Create), "Addresses", new { returnUrl = Url.Action(nameof(Index)) });
        }

        var subtotal = checkoutInput.Value.Lines.Sum(l => l.LineTotal);
        var selectedAddressId = addresses.FirstOrDefault(a => a.IsDefault)?.Id ?? addresses[0].Id;

        return View(new CheckoutAddressPageViewModel(addresses, selectedAddressId, checkoutInput.Value.Lines.Count, subtotal));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(int addressId, CancellationToken cancellationToken)
    {
        var addressResult = await _addressService.GetByIdAsync(UserId, addressId, cancellationToken);
        if (addressResult.IsFailure)
        {
            TempData["Error"] = "Please select a valid address.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Shipping), new { addressId });
    }

    [HttpGet("Shipping")]
    public async Task<IActionResult> Shipping(int addressId, CancellationToken cancellationToken)
    {
        var checkoutInput = await _cartService.GetCheckoutInputAsync(Owner, cancellationToken);
        if (checkoutInput.IsFailure)
        {
            TempData["Error"] = checkoutInput.FirstError.Message;
            return RedirectToAction(nameof(CartController.Index), "Cart");
        }

        var addressResult = await _addressService.GetByIdAsync(UserId, addressId, cancellationToken);
        if (addressResult.IsFailure)
        {
            return RedirectToAction(nameof(Index));
        }

        var address = addressResult.Value;
        var lines = checkoutInput.Value.Lines;

        // Only DiscountedSubtotal is used from this call - Shipping/GrandTotal
        // reflect an auto-picked "cheapest" method that hasn't been chosen
        // yet, but the discount/tax figures don't depend on which shipping
        // method ends up selected, so they're already final.
        var preliminary = await _checkoutCalculationService.CalculateAsync(
            lines, checkoutInput.Value.AppliedPromotionId,
            address.CountryCode, address.RegionCode, address.CountryCode, address.RegionCode,
            selectedShippingMethodId: null, cancellationToken);

        var totalWeight = lines.Sum(l => l.TotalWeight);
        var options = await _shippingService.GetAvailableShippingOptionsAsync(
            totalWeight, preliminary.DiscountedSubtotal, address.CountryCode, address.RegionCode, cancellationToken);

        var selectedShippingMethodId = options.OrderBy(o => o.Cost).FirstOrDefault()?.ShippingMethodId;

        return View(new CheckoutShippingPageViewModel(
            addressId, address, options, selectedShippingMethodId, lines.Count, preliminary.Subtotal));
    }

    [HttpPost("Shipping")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Shipping(int addressId, int shippingMethodId, CancellationToken cancellationToken)
    {
        var addressResult = await _addressService.GetByIdAsync(UserId, addressId, cancellationToken);
        if (addressResult.IsFailure)
        {
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Review), new { addressId, shippingMethodId });
    }

    [HttpGet("Review")]
    public async Task<IActionResult> Review(int addressId, int shippingMethodId, CancellationToken cancellationToken)
    {
        var checkoutInput = await _cartService.GetCheckoutInputAsync(Owner, cancellationToken);
        if (checkoutInput.IsFailure)
        {
            TempData["Error"] = checkoutInput.FirstError.Message;
            return RedirectToAction(nameof(CartController.Index), "Cart");
        }

        var addressResult = await _addressService.GetByIdAsync(UserId, addressId, cancellationToken);
        if (addressResult.IsFailure)
        {
            return RedirectToAction(nameof(Index));
        }

        var address = addressResult.Value;
        var calculation = await _checkoutCalculationService.CalculateAsync(
            checkoutInput.Value.Lines, checkoutInput.Value.AppliedPromotionId,
            address.CountryCode, address.RegionCode, address.CountryCode, address.RegionCode,
            shippingMethodId, cancellationToken);

        // CalculateAsync reports ShippingRateConfigured=false whenever
        // shippingMethodId doesn't match any option available for this
        // address's jurisdiction - a stale selection (cart/address changed
        // since step 2) and "nothing configured at all" both land here and
        // both are correctly resolved by revisiting the Shipping step.
        if (!calculation.ShippingRateConfigured)
        {
            TempData["Error"] = "Please choose a shipping method.";
            return RedirectToAction(nameof(Shipping), new { addressId });
        }

        var totalWeight = checkoutInput.Value.Lines.Sum(l => l.TotalWeight);
        var options = await _shippingService.GetAvailableShippingOptionsAsync(
            totalWeight, calculation.DiscountedSubtotal, address.CountryCode, address.RegionCode, cancellationToken);
        var shippingOption = options.First(o => o.ShippingMethodId == shippingMethodId);

        var cart = await _cartService.GetCartAsync(Owner, cancellationToken);
        var items = cart.Items.Where(i => i.IsAvailable).ToList();

        var idempotencyKey = Guid.NewGuid().ToString("N");
        return View(new CheckoutReviewPageViewModel(address, shippingOption, items, calculation, idempotencyKey));
    }

    /// <summary>
    /// Final submission (Milestone 8.3) - re-runs every check Review already
    /// performed, since time has passed since that page was rendered and any
    /// of cart/stock/address/shipping could have changed, plus the one check
    /// Review doesn't do: stock sufficiency. A duplicate submission carrying
    /// the same idempotencyKey (double-click, browser back-button resubmit,
    /// a retried request) skips straight to replaying the first successful
    /// outcome instead of re-validating - re-validating twice could otherwise
    /// show a confusing failure for a submission the customer already saw
    /// succeed.
    /// </summary>
    [HttpPost("PlaceOrder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(int addressId, int shippingMethodId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(IdempotencyCacheKey(idempotencyKey), out CachedCheckoutResult? cached) &&
            cached is not null && cached.UserId == UserId)
        {
            return RedirectToAction(nameof(Confirmation), new { key = idempotencyKey });
        }

        var checkoutInput = await _cartService.GetCheckoutInputAsync(Owner, cancellationToken);
        if (checkoutInput.IsFailure)
        {
            TempData["Error"] = checkoutInput.FirstError.Message;
            return RedirectToAction(nameof(CartController.Index), "Cart");
        }

        var cart = await _cartService.GetCartAsync(Owner, cancellationToken);
        if (HasStockIssues(cart))
        {
            TempData["Error"] = StockIssuesMessage(cart);
            return RedirectToAction(nameof(CartController.Index), "Cart");
        }

        var addressResult = await _addressService.GetByIdAsync(UserId, addressId, cancellationToken);
        if (addressResult.IsFailure)
        {
            return RedirectToAction(nameof(Index));
        }

        var address = addressResult.Value;
        var calculation = await _checkoutCalculationService.CalculateAsync(
            checkoutInput.Value.Lines, checkoutInput.Value.AppliedPromotionId,
            address.CountryCode, address.RegionCode, address.CountryCode, address.RegionCode,
            shippingMethodId, cancellationToken);

        if (!calculation.ShippingRateConfigured)
        {
            TempData["Error"] = "Please choose a shipping method.";
            return RedirectToAction(nameof(Shipping), new { addressId });
        }

        var totalWeight = checkoutInput.Value.Lines.Sum(l => l.TotalWeight);
        var options = await _shippingService.GetAvailableShippingOptionsAsync(
            totalWeight, calculation.DiscountedSubtotal, address.CountryCode, address.RegionCode, cancellationToken);
        var shippingOption = options.First(o => o.ShippingMethodId == shippingMethodId);

        var items = cart.Items.Where(i => i.IsAvailable).ToList();

        // IMemoryCache is single-instance - fine for this app today, but a
        // multi-instance deployment would need a distributed cache (or a
        // real idempotency table once Milestone 9.1's Order exists to
        // anchor one to). Not worth building ahead of that real need now.
        _cache.Set(IdempotencyCacheKey(idempotencyKey), new CachedCheckoutResult(UserId, address, shippingOption, items, calculation), IdempotencyTtl);

        return RedirectToAction(nameof(Confirmation), new { key = idempotencyKey });
    }

    [HttpGet("Confirmation")]
    public IActionResult Confirmation(string key)
    {
        if (!_cache.TryGetValue(IdempotencyCacheKey(key), out CachedCheckoutResult? entry) || entry is null || entry.UserId != UserId)
        {
            TempData["Error"] = "Your checkout session has expired. Please review your order again.";
            return RedirectToAction(nameof(Index));
        }

        return View(new CheckoutConfirmationPageViewModel(entry.Address, entry.ShippingOption, entry.Items, entry.Calculation));
    }

    private static bool HasStockIssues(CartDto cart) => cart.Items.Any(i => i.IsAvailable && i.QuantityExceedsStock);

    private static string StockIssuesMessage(CartDto cart)
    {
        var names = cart.Items.Where(i => i.IsAvailable && i.QuantityExceedsStock).Select(i => i.ProductName).Distinct();
        return $"Some items in your cart now exceed available stock: {string.Join(", ", names)}. Please update your cart before continuing to checkout.";
    }

    private static string IdempotencyCacheKey(string idempotencyKey) => $"checkout-idempotency:{idempotencyKey}";

    private sealed record CachedCheckoutResult(
        string UserId, AddressDto Address, ShippingOptionDto ShippingOption, IReadOnlyList<CartItemDto> Items, CheckoutCalculationResult Calculation);

    private CartOwner Owner => _cartOwnerAccessor.TryGetOwner()!;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
