using ECommerceApp.Application.Inventory;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageInventory)]
public class PurchaseOrdersController : Controller
{
    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly ISupplierService _supplierService;
    private readonly IInventoryService _inventoryService;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService, ISupplierService supplierService, IInventoryService inventoryService)
    {
        _purchaseOrderService = purchaseOrderService;
        _supplierService = supplierService;
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? status, int page = 1)
    {
        var result = await _purchaseOrderService.GetPagedAsync(new PurchaseOrderQuery
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
    public async Task<IActionResult> Create()
    {
        ViewBag.Suppliers = (await _supplierService.GetAllActiveAsync()).Value;
        ViewBag.Warehouses = (await _inventoryService.GetWarehousesAsync(onlyActive: true)).Value;
        return View(new PurchaseOrderFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseOrderFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Suppliers = (await _supplierService.GetAllActiveAsync()).Value;
            ViewBag.Warehouses = (await _inventoryService.GetWarehousesAsync(onlyActive: true)).Value;
            return View(model);
        }

        var result = await _purchaseOrderService.CreateAsync(new CreatePurchaseOrderRequest(
            model.SupplierId, model.WarehouseId, model.ExpectedDeliveryDate, model.Notes));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            ViewBag.Suppliers = (await _supplierService.GetAllActiveAsync()).Value;
            ViewBag.Warehouses = (await _inventoryService.GetWarehousesAsync(onlyActive: true)).Value;
            return View(model);
        }

        TempData["Message"] = $"Purchase order '{result.Value.OrderNumber}' created.";
        return RedirectToAction(nameof(Edit), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _purchaseOrderService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var linkedProducts = await _supplierService.GetLinkedProductsAsync(result.Value.SupplierId);
        var receipts = await _purchaseOrderService.GetReceiptHistoryAsync(id);

        return View(new PurchaseOrderEditViewModel
        {
            Order = result.Value,
            LinkableProducts = linkedProducts.Value,
            Receipts = receipts.Value,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(AddPurchaseOrderItemViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _purchaseOrderService.AddItemAsync(new AddPurchaseOrderItemRequest(
                model.PurchaseOrderId, model.ProductId, model.QuantityOrdered, model.UnitCost));

            if (result.IsFailure)
            {
                TempData["Error"] = result.FirstError.Message;
            }
        }

        return RedirectToAction(nameof(Edit), new { id = model.PurchaseOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(int purchaseOrderItemId, int purchaseOrderId)
    {
        var result = await _purchaseOrderService.RemoveItemAsync(purchaseOrderItemId);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }

        return RedirectToAction(nameof(Edit), new { id = purchaseOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var result = await _purchaseOrderService.SubmitAsync(id);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }
        else
        {
            TempData["Message"] = "Purchase order submitted.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var result = await _purchaseOrderService.ApproveAsync(id);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }
        else
        {
            TempData["Message"] = "Purchase order approved.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _purchaseOrderService.CancelAsync(id);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }
        else
        {
            TempData["Message"] = "Purchase order cancelled.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Receive(int id)
    {
        var result = await _purchaseOrderService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var order = result.Value;
        if (order.Status is not ("Approved" or "PartiallyReceived"))
        {
            TempData["Error"] = "Only an approved or partially received purchase order can receive goods.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var model = new ReceiveGoodsViewModel
        {
            PurchaseOrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Lines = order.Items
                .Where(i => i.QuantityReceived < i.QuantityOrdered)
                .Select(i => new ReceiveGoodsLineViewModel
                {
                    PurchaseOrderItemId = i.Id,
                    ProductName = i.ProductName,
                    Outstanding = i.QuantityOrdered - i.QuantityReceived,
                })
                .ToList(),
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(ReceiveGoodsViewModel model)
    {
        var lines = model.Lines
            .Where(l => l.QuantityReceived > 0)
            .Select(l => new ReceiveGoodsLineRequest(l.PurchaseOrderItemId, l.QuantityReceived, l.AllowOverride))
            .ToList();

        if (lines.Count == 0)
        {
            TempData["Error"] = "Enter a quantity to receive for at least one line.";
            return RedirectToAction(nameof(Receive), new { id = model.PurchaseOrderId });
        }

        var result = await _purchaseOrderService.ReceiveAsync(new ReceiveGoodsRequest(
            model.PurchaseOrderId, lines, model.Notes, model.OverrideReason));

        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
            return RedirectToAction(nameof(Receive), new { id = model.PurchaseOrderId });
        }

        TempData["Message"] = "Goods received.";
        return RedirectToAction(nameof(Edit), new { id = model.PurchaseOrderId });
    }
}
