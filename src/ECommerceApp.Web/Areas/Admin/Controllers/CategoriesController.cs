using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageCatalog)]
public class CategoriesController : Controller
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? sortBy, bool sortDescending = false, int page = 1)
    {
        var result = await _categoryService.GetPagedAsync(new PagedQuery
        {
            Page = page,
            PageSize = 20,
            Search = search,
            SortBy = sortBy,
            SortDescending = sortDescending,
        });

        ViewData["Search"] = search;
        ViewData["SortBy"] = sortBy;
        ViewData["SortDescending"] = sortDescending;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Tree()
    {
        var result = await _categoryService.GetTreeAsync();
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Deleted(int page = 1)
    {
        var result = await _categoryService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, OnlyDeleted = true });
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CategoryFormViewModel { AvailableParents = await AvailableParentsAsync(null) };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableParents = await AvailableParentsAsync(null);
            return View(model);
        }

        var result = await _categoryService.CreateAsync(new CreateCategoryRequest(
            model.Name, model.Slug, model.Description, model.ParentCategoryId, model.DisplayOrder, model.IsActive, model.IsFeatured));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            model.AvailableParents = await AvailableParentsAsync(null);
            return View(model);
        }

        TempData["Message"] = $"Category '{result.Value.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _categoryService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var category = result.Value;
        var model = new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ParentCategoryId = category.ParentCategoryId,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            IsFeatured = category.IsFeatured,
            AvailableParents = await AvailableParentsAsync(id),
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableParents = await AvailableParentsAsync(model.Id);
            return View(model);
        }

        var result = await _categoryService.UpdateAsync(new UpdateCategoryRequest(
            model.Id, model.Name, model.Slug, model.Description, model.ParentCategoryId, model.DisplayOrder, model.IsActive, model.IsFeatured));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            model.AvailableParents = await AvailableParentsAsync(model.Id);
            return View(model);
        }

        TempData["Message"] = $"Category '{result.Value.Name}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _categoryService.DeactivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _categoryService.ActivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _categoryService.DeleteAsync(id);
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
        await _categoryService.RestoreAsync(id);
        return RedirectToAction(nameof(Deleted));
    }

    private async Task<IEnumerable<CategoryDto>> AvailableParentsAsync(int? excludeId)
    {
        var result = await _categoryService.GetAllActiveAsync();
        return excludeId.HasValue ? result.Value.Where(c => c.Id != excludeId.Value) : result.Value;
    }
}
