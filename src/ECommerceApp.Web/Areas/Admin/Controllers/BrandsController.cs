using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageCatalog)]
public class BrandsController : Controller
{
    private readonly IBrandService _brandService;
    private readonly IFileStorage _fileStorage;

    public BrandsController(IBrandService brandService, IFileStorage fileStorage)
    {
        _brandService = brandService;
        _fileStorage = fileStorage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? sortBy, bool sortDescending = false, int page = 1)
    {
        var result = await _brandService.GetPagedAsync(new PagedQuery
        {
            Page = page,
            PageSize = 20,
            Search = search,
            SortBy = sortBy,
            SortDescending = sortDescending,
        });

        ViewData["Search"] = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Deleted(int page = 1)
    {
        var result = await _brandService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, OnlyDeleted = true });
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create() => View(new BrandFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BrandFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _brandService.CreateAsync(new CreateBrandRequest(
            model.Name, model.Slug, model.Description, model.Website, model.IsActive, model.IsFeatured));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Brand '{result.Value.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _brandService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var brand = result.Value;
        return View(new BrandFormViewModel
        {
            Id = brand.Id,
            Name = brand.Name,
            Slug = brand.Slug,
            Description = brand.Description,
            Website = brand.Website,
            IsActive = brand.IsActive,
            IsFeatured = brand.IsFeatured,
            LogoPath = brand.LogoPath,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(BrandFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _brandService.UpdateAsync(new UpdateBrandRequest(
            model.Id, model.Name, model.Slug, model.Description, model.Website, model.IsActive, model.IsFeatured));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Brand '{result.Value.Name}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadLogo(int id, IFormFile file)
    {
        if (file.Length == 0)
        {
            TempData["Error"] = "Please choose a file to upload.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        await using var stream = file.OpenReadStream();
        var saveResult = await _fileStorage.SaveImageAsync(stream, file.FileName, file.ContentType, "brands");
        if (saveResult.IsFailure)
        {
            TempData["Error"] = saveResult.FirstError.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }

        await _brandService.SetLogoAsync(id, saveResult.Value);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _brandService.DeactivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _brandService.ActivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _brandService.DeleteAsync(id);
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
        await _brandService.RestoreAsync(id);
        return RedirectToAction(nameof(Deleted));
    }
}
