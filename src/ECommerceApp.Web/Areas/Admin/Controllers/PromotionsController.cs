using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Marketing;
using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageCatalog)]
public class PromotionsController : Controller
{
    private readonly IPromotionService _promotionService;
    private readonly ICategoryService _categoryService;
    private readonly IBrandService _brandService;
    private readonly IProductService _productService;

    public PromotionsController(
        IPromotionService promotionService, ICategoryService categoryService, IBrandService brandService, IProductService productService)
    {
        _promotionService = promotionService;
        _categoryService = categoryService;
        _brandService = brandService;
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var result = await _promotionService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, Search = search });
        ViewData["Search"] = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Deleted(int page = 1)
    {
        var result = await _promotionService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, OnlyDeleted = true });
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new PromotionFormViewModel();
        await PopulateScopeOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PromotionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateScopeOptionsAsync(model);
            return View(model);
        }

        var result = await _promotionService.CreateAsync(new CreatePromotionRequest(
            model.Name, model.Description, model.CouponCode, model.DiscountType, model.DiscountValue,
            model.ScopeType, model.ScopeCategoryId, model.ScopeBrandId, model.ScopeProductId,
            model.MinimumOrderAmount, model.MaxDiscountAmount, model.StartsAtUtc, model.EndsAtUtc,
            model.MaxTotalUses, model.MaxUsesPerCustomer, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            await PopulateScopeOptionsAsync(model);
            return View(model);
        }

        TempData["Message"] = $"Promotion '{result.Value.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _promotionService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var promotion = result.Value;
        var model = new PromotionFormViewModel
        {
            Id = promotion.Id,
            Name = promotion.Name,
            Description = promotion.Description,
            CouponCode = promotion.CouponCode,
            DiscountType = promotion.DiscountType,
            DiscountValue = promotion.DiscountValue,
            ScopeType = promotion.ScopeType,
            ScopeCategoryId = promotion.ScopeCategoryId,
            ScopeBrandId = promotion.ScopeBrandId,
            ScopeProductId = promotion.ScopeProductId,
            MinimumOrderAmount = promotion.MinimumOrderAmount,
            MaxDiscountAmount = promotion.MaxDiscountAmount,
            StartsAtUtc = promotion.StartsAtUtc,
            EndsAtUtc = promotion.EndsAtUtc,
            MaxTotalUses = promotion.MaxTotalUses,
            MaxUsesPerCustomer = promotion.MaxUsesPerCustomer,
            IsActive = promotion.IsActive,
        };
        await PopulateScopeOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PromotionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateScopeOptionsAsync(model);
            return View(model);
        }

        var result = await _promotionService.UpdateAsync(new UpdatePromotionRequest(
            model.Id, model.Name, model.Description, model.CouponCode, model.DiscountType, model.DiscountValue,
            model.ScopeType, model.ScopeCategoryId, model.ScopeBrandId, model.ScopeProductId,
            model.MinimumOrderAmount, model.MaxDiscountAmount, model.StartsAtUtc, model.EndsAtUtc,
            model.MaxTotalUses, model.MaxUsesPerCustomer, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            await PopulateScopeOptionsAsync(model);
            return View(model);
        }

        TempData["Message"] = $"Promotion '{result.Value.Name}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _promotionService.ActivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _promotionService.DeactivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _promotionService.DeleteAsync(id);
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
        await _promotionService.RestoreAsync(id);
        return RedirectToAction(nameof(Deleted));
    }

    private async Task PopulateScopeOptionsAsync(PromotionFormViewModel model)
    {
        model.AvailableCategories = (await _categoryService.GetAllActiveAsync()).Value;
        model.AvailableBrands = (await _brandService.GetAllActiveAsync()).Value;
        model.AvailableProducts = (await _productService.GetPickerListAsync()).Value;
    }
}
