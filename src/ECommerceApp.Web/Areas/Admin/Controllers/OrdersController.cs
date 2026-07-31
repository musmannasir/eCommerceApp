using ECommerceApp.Application.Orders;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageOrders)]
public class OrdersController : Controller
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? status, int page = 1)
    {
        var result = await _orderService.GetPagedAsync(new OrderQuery
        {
            Page = page,
            PageSize = 20,
            Search = search,
            Status = status,
        });

        ViewData["Search"] = search;
        ViewData["Status"] = status;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _orderService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _orderService.CancelAsync(id);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }
        else
        {
            TempData["Message"] = "Order cancelled.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotes(int id, string? notes)
    {
        var result = await _orderService.UpdateAdminNotesAsync(id, notes);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }
        else
        {
            TempData["Message"] = "Notes saved.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ship(int id, string carrier, string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(carrier) || string.IsNullOrWhiteSpace(trackingNumber))
        {
            TempData["Error"] = "Carrier and tracking number are both required.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _orderService.ShipAsync(id, new ShipOrderRequest(carrier, trackingNumber));
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }
        else
        {
            TempData["Message"] = "Order marked as shipped.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDelivered(int id)
    {
        var result = await _orderService.MarkDeliveredAsync(id);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }
        else
        {
            TempData["Message"] = "Order marked as delivered.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
