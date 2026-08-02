using System.Security.Claims;
using ECommerceApp.Application.Carts;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Orders;
using ECommerceApp.Application.Returns;
using ECommerceApp.Application.Returns.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Orders;
using ECommerceApp.Domain.Payments;
using ECommerceApp.Web.Models.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers;

/// <summary>
/// Customer-facing "My Orders" dashboard, list, detail, tracking, invoice,
/// and reorder (Milestones 11.1-11.3) - ownership-scoped to the signed-in
/// customer, unlike the admin order queue/detail (Milestones 10.1/10.2).
/// Account-only, like Addresses/Wishlist - no guest concept, since order
/// history only makes sense once you're signed in. Details/Invoice both
/// reuse the same ownership-scoped IOrderService.GetByOrderNumberAsync
/// that has existed since Milestone 9.1 - no new Application-layer work
/// was needed, since Milestone 10.3 already put shipment/tracking fields
/// on OrderDto. CartOwner.ForUser(UserId) is constructed directly rather
/// than via ICartOwnerAccessor - [Authorize] already guarantees a real
/// signed-in user, so there's no guest-cart case to resolve here.
/// </summary>
[Authorize]
[Route("Orders")]
public class OrdersController : Controller
{
    private readonly IOrderService _orderService;
    private readonly ICartService _cartService;
    private readonly IReturnService _returnService;

    public OrdersController(IOrderService orderService, ICartService cartService, IReturnService returnService)
    {
        _orderService = orderService;
        _cartService = cartService;
        _returnService = returnService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _orderService.GetDashboardAsync(UserId, page, 10, cancellationToken);
        return View(result.Value);
    }

    [HttpGet("{orderNumber}")]
    public async Task<IActionResult> Details(string orderNumber, CancellationToken cancellationToken)
    {
        var result = await _orderService.GetByOrderNumberAsync(UserId, orderNumber, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        return View(result.Value);
    }

    /// <summary>Only meaningful for an order that was actually charged - there's nothing to invoice for a failed one.</summary>
    [HttpGet("{orderNumber}/Invoice")]
    public async Task<IActionResult> Invoice(string orderNumber, CancellationToken cancellationToken)
    {
        var result = await _orderService.GetByOrderNumberAsync(UserId, orderNumber, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        if (result.Value.PaymentStatus != nameof(PaymentStatus.Succeeded))
        {
            TempData["Error"] = "An invoice is only available for an order that was successfully charged.";
            return RedirectToAction(nameof(Details), new { orderNumber });
        }

        return View(result.Value);
    }

    /// <summary>
    /// Not gated by order status - even a failed order's items are worth
    /// re-adding to try again. Redirects to the cart so the customer sees
    /// the outcome (added count, or a per-item reason for any skipped)
    /// immediately via the TempData banner Cart/Index already renders.
    /// </summary>
    [HttpPost("{orderNumber}/Reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(string orderNumber, CancellationToken cancellationToken)
    {
        var orderResult = await _orderService.GetByOrderNumberAsync(UserId, orderNumber, cancellationToken);
        if (orderResult.IsFailure)
        {
            return NotFound();
        }

        var items = orderResult.Value.Items
            .Select(i => new ReorderItemRequest(i.ProductId, i.ProductVariantId, i.Quantity, i.ProductName))
            .ToList();

        var owner = CartOwner.ForUser(UserId);
        var result = await _cartService.ReorderAsync(owner, items, cancellationToken);

        if (result.SkippedItems.Count == 0)
        {
            TempData["Message"] = result.AddedCount == 1
                ? "Added 1 item to your cart."
                : $"Added {result.AddedCount} items to your cart.";
        }
        else if (result.AddedCount == 0)
        {
            TempData["Error"] = "None of this order's items could be added to your cart: "
                + string.Join(' ', result.SkippedItems.Select(s => $"{s.ProductName} - {s.Reason}"));
        }
        else
        {
            TempData["Message"] = $"Added {result.AddedCount} item(s) to your cart.";
            TempData["Error"] = "Some items could not be added: "
                + string.Join(' ', result.SkippedItems.Select(s => $"{s.ProductName} - {s.Reason}"));
        }

        return RedirectToAction("Index", "Cart");
    }

    /// <summary>
    /// Self-service cancellation (Milestone 13.1) - only a Paid order (per
    /// OrderStatusTransitions), the same rule and reservation-release logic
    /// the admin Cancel action (Milestone 10.2) uses, just ownership-scoped.
    /// Still no refund (Milestone 13.3's job).
    /// </summary>
    [HttpPost("{orderNumber}/Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string orderNumber, CancellationToken cancellationToken)
    {
        var result = await _orderService.CancelOwnOrderAsync(UserId, orderNumber, cancellationToken);
        if (result.IsFailure && result.FirstError.Type == ErrorType.NotFound)
        {
            return NotFound();
        }

        TempData[result.IsSuccess ? "Message" : "Error"] = result.IsSuccess ? "Order cancelled." : result.FirstError.Message;
        return RedirectToAction(nameof(Details), new { orderNumber });
    }

    /// <summary>
    /// Only a Delivered order with no open (Requested/Approved) return
    /// request is offered a form - other cases redirect back to Details
    /// with an explanatory message rather than showing a pointless form.
    /// </summary>
    [HttpGet("{orderNumber}/Return")]
    public async Task<IActionResult> Return(string orderNumber, CancellationToken cancellationToken)
    {
        var result = await _orderService.GetByOrderNumberAsync(UserId, orderNumber, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var order = result.Value;
        if (order.Status != nameof(OrderStatus.Delivered))
        {
            TempData["Error"] = "Only a delivered order can be returned.";
            return RedirectToAction(nameof(Details), new { orderNumber });
        }

        if (order.ReturnRequests.Any(r => r.Status is ReturnRequestStatus.Requested or ReturnRequestStatus.Approved))
        {
            TempData["Error"] = "A return request for this order is already pending.";
            return RedirectToAction(nameof(Details), new { orderNumber });
        }

        var model = new ReturnRequestFormViewModel
        {
            Items = order.Items.Select(i => new ReturnRequestFormItemViewModel
            {
                OrderItemId = i.Id,
                ProductName = i.ProductName,
                OrderedQuantity = i.Quantity,
            }).ToList(),
        };

        ViewData["OrderNumber"] = orderNumber;
        return View(model);
    }

    [HttpPost("{orderNumber}/Return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReturn(string orderNumber, ReturnRequestFormViewModel model, CancellationToken cancellationToken)
    {
        var items = model.Items
            .Where(i => i.Quantity > 0)
            .Select(i => new CreateReturnRequestItem(i.OrderItemId, i.Quantity))
            .ToList();

        var request = new CreateReturnRequestRequest(orderNumber, model.Reason, model.Comment, items);
        var result = await _returnService.SubmitReturnRequestAsync(UserId, request, cancellationToken);

        if (result.IsFailure && result.FirstError.Type == ErrorType.NotFound)
        {
            return NotFound();
        }

        TempData[result.IsSuccess ? "Message" : "Error"] = result.IsSuccess
            ? "Your return request has been submitted."
            : result.FirstError.Message;

        return RedirectToAction(nameof(Details), new { orderNumber });
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
