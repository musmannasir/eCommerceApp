using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Taxation;
using ECommerceApp.Application.Taxation.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageCatalog)]
public class TaxRatesController : Controller
{
    private readonly ITaxService _taxService;

    public TaxRatesController(ITaxService taxService)
    {
        _taxService = taxService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var result = await _taxService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, Search = search });
        ViewData["Search"] = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Deleted(int page = 1)
    {
        var result = await _taxService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, OnlyDeleted = true });
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create() => View(new TaxRateFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaxRateFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _taxService.CreateAsync(new CreateTaxRateRequest(
            model.CountryCode, model.RegionCode, model.TaxCategory, model.RatePercent, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Tax rate for {result.Value.CountryCode}/{result.Value.TaxCategory} created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _taxService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var rate = result.Value;
        return View(new TaxRateFormViewModel
        {
            Id = rate.Id,
            CountryCode = rate.CountryCode,
            RegionCode = rate.RegionCode,
            TaxCategory = rate.TaxCategory,
            RatePercent = rate.RatePercent,
            IsActive = rate.IsActive,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TaxRateFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _taxService.UpdateAsync(new UpdateTaxRateRequest(
            model.Id, model.CountryCode, model.RegionCode, model.TaxCategory, model.RatePercent, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Tax rate for {result.Value.CountryCode}/{result.Value.TaxCategory} updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _taxService.ActivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _taxService.DeactivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _taxService.DeleteAsync(id);
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
        await _taxService.RestoreAsync(id);
        return RedirectToAction(nameof(Deleted));
    }
}
