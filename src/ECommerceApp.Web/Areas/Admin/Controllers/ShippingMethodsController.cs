using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Shipping;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageCatalog)]
public class ShippingMethodsController : Controller
{
    private readonly IShippingService _shippingService;

    public ShippingMethodsController(IShippingService shippingService)
    {
        _shippingService = shippingService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var result = await _shippingService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, Search = search });
        ViewData["Search"] = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Deleted(int page = 1)
    {
        var result = await _shippingService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, OnlyDeleted = true });
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create() => View(new ShippingMethodFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShippingMethodFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _shippingService.CreateAsync(new CreateShippingMethodRequest(
            model.Name, model.Description, model.CountryCode, model.RegionCode, model.BaseRate, model.RatePerKg,
            model.FreeShippingThreshold, model.EstimatedDeliveryDaysMin, model.EstimatedDeliveryDaysMax,
            model.DisplayOrder, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Shipping method '{result.Value.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _shippingService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var method = result.Value;
        return View(new ShippingMethodFormViewModel
        {
            Id = method.Id,
            Name = method.Name,
            Description = method.Description,
            CountryCode = method.CountryCode,
            RegionCode = method.RegionCode,
            BaseRate = method.BaseRate,
            RatePerKg = method.RatePerKg,
            FreeShippingThreshold = method.FreeShippingThreshold,
            EstimatedDeliveryDaysMin = method.EstimatedDeliveryDaysMin,
            EstimatedDeliveryDaysMax = method.EstimatedDeliveryDaysMax,
            DisplayOrder = method.DisplayOrder,
            IsActive = method.IsActive,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ShippingMethodFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _shippingService.UpdateAsync(new UpdateShippingMethodRequest(
            model.Id, model.Name, model.Description, model.CountryCode, model.RegionCode, model.BaseRate, model.RatePerKg,
            model.FreeShippingThreshold, model.EstimatedDeliveryDaysMin, model.EstimatedDeliveryDaysMax,
            model.DisplayOrder, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Shipping method '{result.Value.Name}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _shippingService.ActivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _shippingService.DeactivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _shippingService.DeleteAsync(id);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        await _shippingService.RestoreAsync(id);
        return RedirectToAction(nameof(Deleted));
    }
}
