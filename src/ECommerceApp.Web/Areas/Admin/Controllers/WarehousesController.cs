using ECommerceApp.Application.Inventory;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageInventory)]
public class WarehousesController : Controller
{
    private readonly IInventoryService _inventoryService;

    public WarehousesController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _inventoryService.GetWarehousesAsync();
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create() => View(new WarehouseFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WarehouseFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _inventoryService.CreateWarehouseAsync(new CreateWarehouseRequest(
            model.Name, model.Code, model.AddressLine1, model.AddressLine2, model.City, model.Region, model.PostalCode, model.Country, model.IsDefault, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Warehouse '{result.Value.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _inventoryService.GetWarehouseByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var warehouse = result.Value;
        var model = new WarehouseFormViewModel
        {
            Id = warehouse.Id,
            Name = warehouse.Name,
            Code = warehouse.Code,
            AddressLine1 = warehouse.AddressLine1,
            AddressLine2 = warehouse.AddressLine2,
            City = warehouse.City,
            Region = warehouse.Region,
            PostalCode = warehouse.PostalCode,
            Country = warehouse.Country,
            IsDefault = warehouse.IsDefault,
            IsActive = warehouse.IsActive,
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(WarehouseFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _inventoryService.UpdateWarehouseAsync(new UpdateWarehouseRequest(
            model.Id, model.Name, model.Code, model.AddressLine1, model.AddressLine2, model.City, model.Region, model.PostalCode, model.Country, model.IsDefault, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Warehouse '{result.Value.Name}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _inventoryService.DeactivateWarehouseAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _inventoryService.ActivateWarehouseAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
