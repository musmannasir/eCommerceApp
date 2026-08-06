using System.Security.Claims;
using ECommerceApp.Application.Configuration;
using ECommerceApp.Application.Configuration.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

/// <summary>
/// Milestone 16.3 - a single admin-editable row of store-wide settings that
/// used to live only in appsettings.json's static "Store" section. Unlike
/// every other Admin controller (list/CRUD over many rows), this is just an
/// Index GET+POST pair over the one settings row. Gated by CanManageUsers -
/// the same tightest-tier judgment call Milestone 16.2's audit log made,
/// since there's no dedicated "manage store configuration" policy.
/// </summary>
[Area("Admin")]
[Authorize(Policy = Policies.CanManageUsers)]
public class SettingsController : Controller
{
    private readonly IStoreSettingsService _storeSettingsService;

    public SettingsController(IStoreSettingsService storeSettingsService)
    {
        _storeSettingsService = storeSettingsService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await _storeSettingsService.GetAsync(cancellationToken);
        return View(ToViewModel(settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(StoreSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new UpdateStoreSettingsRequest(
            model.StoreName, model.Currency, model.DefaultCountry, model.PricesIncludeTax, model.RecentlyViewedMaxItems,
            model.DefaultTaxCountryCode, model.DefaultTaxRegionCode, model.DefaultShippingCountryCode, model.DefaultShippingRegionCode,
            Convert.FromBase64String(model.RowVersion));

        var result = await _storeSettingsService.UpdateAsync(request, CurrentUserId, cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = "Store settings updated.";
        return RedirectToAction(nameof(Index));
    }

    private static StoreSettingsViewModel ToViewModel(StoreSettingsDto dto) => new()
    {
        StoreName = dto.StoreName,
        Currency = dto.Currency,
        DefaultCountry = dto.DefaultCountry,
        PricesIncludeTax = dto.PricesIncludeTax,
        RecentlyViewedMaxItems = dto.RecentlyViewedMaxItems,
        DefaultTaxCountryCode = dto.DefaultTaxCountryCode,
        DefaultTaxRegionCode = dto.DefaultTaxRegionCode,
        DefaultShippingCountryCode = dto.DefaultShippingCountryCode,
        DefaultShippingRegionCode = dto.DefaultShippingRegionCode,
        RowVersion = Convert.ToBase64String(dto.RowVersion),
    };

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
