using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Inventory;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageInventory)]
public class InventoryController : Controller
{
    private const int PageSize = 20;

    private readonly IInventoryService _inventoryService;
    private readonly IProductService _productService;

    public InventoryController(IInventoryService inventoryService, IProductService productService)
    {
        _inventoryService = inventoryService;
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? warehouseId, string? search, int page = 1)
    {
        var result = await _inventoryService.GetOverviewAsync(new InventoryItemQuery
        {
            Page = page,
            PageSize = PageSize,
            WarehouseId = warehouseId,
            Search = search,
        });

        await PopulateFilterDataAsync(warehouseId, search);
        ViewData["Title"] = "Inventory Overview";
        ViewData["ActionName"] = nameof(Index);
        return View("ItemList", result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> LowStock(int? warehouseId, int page = 1)
    {
        var result = await _inventoryService.GetOverviewAsync(new InventoryItemQuery
        {
            Page = page,
            PageSize = PageSize,
            WarehouseId = warehouseId,
            OnlyLowStock = true,
        });

        await PopulateFilterDataAsync(warehouseId, null);
        ViewData["Title"] = "Low Stock";
        ViewData["ActionName"] = nameof(LowStock);
        return View("ItemList", result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> OutOfStock(int? warehouseId, int page = 1)
    {
        var result = await _inventoryService.GetOverviewAsync(new InventoryItemQuery
        {
            Page = page,
            PageSize = PageSize,
            WarehouseId = warehouseId,
            OnlyOutOfStock = true,
        });

        await PopulateFilterDataAsync(warehouseId, null);
        ViewData["Title"] = "Out of Stock";
        ViewData["ActionName"] = nameof(OutOfStock);
        return View("ItemList", result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> MovementHistory(int id, int page = 1)
    {
        var itemResult = await _inventoryService.GetInventoryItemByIdAsync(id);
        if (itemResult.IsFailure)
        {
            return NotFound();
        }

        var movementsResult = await _inventoryService.GetMovementHistoryAsync(id, new PagedQuery { Page = page, PageSize = PageSize });

        ViewData["Item"] = itemResult.Value;
        return View(movementsResult.Value);
    }

    [HttpGet]
    public async Task<IActionResult> OpeningStock()
    {
        var model = new OpeningStockFormViewModel();
        await PopulateOpeningStockPickersAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpeningStock(OpeningStockFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOpeningStockPickersAsync(model);
            return View(model);
        }

        var result = await _inventoryService.RecordOpeningStockAsync(new RecordOpeningStockRequest(
            model.WarehouseId, model.ProductId, model.ProductVariantId, model.Quantity, model.ReorderLevel, model.ReorderQuantity, model.AllowBackorder));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            await PopulateOpeningStockPickersAsync(model);
            return View(model);
        }

        TempData["Message"] = $"Opening stock recorded for '{result.Value.ProductName}' ({result.Value.Sku}).";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Adjust(int id)
    {
        var result = await _inventoryService.GetInventoryItemByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var item = result.Value;
        var model = new AdjustStockFormViewModel
        {
            InventoryItemId = item.Id,
            ProductName = item.ProductName,
            Sku = item.Sku,
            WarehouseName = item.WarehouseName,
            CurrentQuantityOnHand = item.QuantityOnHand,
            CurrentQuantityAvailable = item.QuantityAvailable,
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(AdjustStockFormViewModel model)
    {
        if (model.QuantityDelta == 0)
        {
            ModelState.AddModelError(nameof(model.QuantityDelta), "Adjustment quantity cannot be zero.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _inventoryService.AdjustStockAsync(new AdjustStockRequest(model.InventoryItemId, model.QuantityDelta, model.Reason));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Stock adjusted for '{result.Value.ProductName}' ({result.Value.Sku}). New on-hand: {result.Value.QuantityOnHand}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateFilterDataAsync(int? warehouseId, string? search)
    {
        var warehouses = await _inventoryService.GetWarehousesAsync();
        ViewData["Warehouses"] = warehouses.Value;
        ViewData["WarehouseId"] = warehouseId;
        ViewData["Search"] = search;
    }

    private async Task PopulateOpeningStockPickersAsync(OpeningStockFormViewModel model)
    {
        var warehouses = await _inventoryService.GetWarehousesAsync(onlyActive: true);
        model.AvailableWarehouses = warehouses.Value;

        var products = await _productService.GetPickerListAsync();
        model.AvailableProducts = products.Value
            .Select(p => new ProductPickerDto(p.Id, p.Name, p.BaseSKU, p.Variants.Select(v => new ProductVariantPickerDto(v.Id, v.SKU)).ToList()))
            .ToList();
    }
}
