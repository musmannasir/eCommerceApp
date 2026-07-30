using System.Security.Claims;
using ECommerceApp.Application.Addresses;
using ECommerceApp.Application.Carts;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout;
using ECommerceApp.Application.Orders;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Application.Payments.Models;
using ECommerceApp.Application.Shipping;
using ECommerceApp.Domain.Payments;
using ECommerceApp.Web.Models.Checkout;
using ECommerceApp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
/// Milestone 8.3 added server-side revalidation and idempotency to the final
/// submission. Milestone 9.1 replaces the IMemoryCache-based idempotency
/// token with a real, durable one - Order.IdempotencyKey (unique-indexed) -
/// and PlaceOrder now actually persists an Order instead of caching a DTO.
/// Stock is still not reserved or deducted here (Milestone 9.3's job); the
/// stock-sufficiency check below remains a best-effort guard only.
/// Milestone 9.2 charges a (simulated) card as part of the same PlaceOrder
/// submission - a declined card still leaves a real, placed order (visible
/// on Confirmation, marked PaymentFailed) but the cart is only cleared once
/// payment actually succeeds, so a customer whose card was declined can
/// immediately retry checkout with the same cart contents.
/// </summary>
[Authorize]
[Route("Checkout")]
public class CheckoutController : Controller
{
    private readonly ICartService _cartService;
    private readonly IAddressService _addressService;
    private readonly IShippingService _shippingService;
    private readonly ICheckoutCalculationService _checkoutCalculationService;
    private readonly IOrderService _orderService;
    private readonly ICartOwnerAccessor _cartOwnerAccessor;

    public CheckoutController(
        ICartService cartService, IAddressService addressService, IShippingService shippingService,
        ICheckoutCalculationService checkoutCalculationService, IOrderService orderService, ICartOwnerAccessor cartOwnerAccessor)
    {
        _cartService = cartService;
        _addressService = addressService;
        _shippingService = shippingService;
        _checkoutCalculationService = checkoutCalculationService;
        _orderService = orderService;
        _cartOwnerAccessor = cartOwnerAccessor;
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
    /// Final submission (Milestone 8.3, now persisting a real Order as of
    /// Milestone 9.1) - re-runs every check Review already performed, since
    /// time has passed since that page was rendered and any of
    /// cart/stock/address/shipping could have changed, plus the one check
    /// Review doesn't do: stock sufficiency. A duplicate submission carrying
    /// the same idempotencyKey (double-click, browser back-button resubmit,
    /// a retried request) skips straight to replaying the order already
    /// created for that key instead of re-validating - re-validating twice
    /// could otherwise show a confusing failure for a submission the
    /// customer already saw succeed.
    /// </summary>
    [HttpPost("PlaceOrder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(
        int addressId, int shippingMethodId, string idempotencyKey,
        string cardNumber, string cardholderName, int expiryMonth, int expiryYear, string cvv,
        CancellationToken cancellationToken)
    {
        var existingOrder = await _orderService.GetByIdempotencyKeyAsync(UserId, idempotencyKey, cancellationToken);
        if (existingOrder.IsSuccess)
        {
            return RedirectToAction(nameof(Confirmation), new { orderNumber = existingOrder.Value.OrderNumber });
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
        var payment = new ChargeRequest(cardNumber, cardholderName, expiryMonth, expiryYear, cvv, calculation.GrandTotal);

        var orderResult = await _orderService.CreateOrderAsync(
            new CreateOrderRequest(UserId, idempotencyKey, address, checkoutInput.Value.AppliedPromotionId, shippingOption, items, calculation, payment),
            cancellationToken);

        if (orderResult.IsFailure)
        {
            TempData["Error"] = orderResult.FirstError.Message;
            return RedirectToAction(nameof(Review), new { addressId, shippingMethodId });
        }

        // Only clear the cart once the card was actually charged - a
        // declined card leaves the cart intact so the customer can retry
        // immediately rather than having to re-add everything.
        if (orderResult.Value.PaymentStatus == nameof(PaymentStatus.Succeeded))
        {
            await _cartService.ClearCartAsync(Owner, cancellationToken);
        }

        return RedirectToAction(nameof(Confirmation), new { orderNumber = orderResult.Value.OrderNumber });
    }

    [HttpGet("Confirmation/{orderNumber}")]
    public async Task<IActionResult> Confirmation(string orderNumber, CancellationToken cancellationToken)
    {
        var orderResult = await _orderService.GetByOrderNumberAsync(UserId, orderNumber, cancellationToken);
        if (orderResult.IsFailure)
        {
            TempData["Error"] = "We couldn't find that order.";
            return RedirectToAction(nameof(Index));
        }

        return View(new CheckoutConfirmationPageViewModel(orderResult.Value));
    }

    private static bool HasStockIssues(CartDto cart) => cart.Items.Any(i => i.IsAvailable && i.QuantityExceedsStock);

    private static string StockIssuesMessage(CartDto cart)
    {
        var names = cart.Items.Where(i => i.IsAvailable && i.QuantityExceedsStock).Select(i => i.ProductName).Distinct();
        return $"Some items in your cart now exceed available stock: {string.Join(", ", names)}. Please update your cart before continuing to checkout.";
    }

    private CartOwner Owner => _cartOwnerAccessor.TryGetOwner()!;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
