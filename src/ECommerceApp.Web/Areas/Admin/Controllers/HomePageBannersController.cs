using ECommerceApp.Application.Common.Interfaces;
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
public class HomePageBannersController : Controller
{
    private readonly IHomePageBannerService _bannerService;
    private readonly IFileStorage _fileStorage;

    public HomePageBannersController(IHomePageBannerService bannerService, IFileStorage fileStorage)
    {
        _bannerService = bannerService;
        _fileStorage = fileStorage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var result = await _bannerService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, Search = search });
        ViewData["Search"] = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Deleted(int page = 1)
    {
        var result = await _bannerService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, OnlyDeleted = true });
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create() => View(new HomePageBannerFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HomePageBannerFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _bannerService.CreateAsync(new CreateHomePageBannerRequest(
            model.Title, model.Subtitle, model.LinkUrl, model.BannerType, model.DisplayOrder, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Banner '{result.Value.Title}' created. Upload an image to make it visible on the home page.";
        return RedirectToAction(nameof(Edit), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _bannerService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var banner = result.Value;
        return View(new HomePageBannerFormViewModel
        {
            Id = banner.Id,
            Title = banner.Title,
            Subtitle = banner.Subtitle,
            LinkUrl = banner.LinkUrl,
            BannerType = banner.BannerType,
            DisplayOrder = banner.DisplayOrder,
            IsActive = banner.IsActive,
            ImagePath = banner.ImagePath,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(HomePageBannerFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _bannerService.UpdateAsync(new UpdateHomePageBannerRequest(
            model.Id, model.Title, model.Subtitle, model.LinkUrl, model.BannerType, model.DisplayOrder, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Banner '{result.Value.Title}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(int id, IFormFile file)
    {
        if (file.Length == 0)
        {
            TempData["Error"] = "Please choose a file to upload.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        await using var stream = file.OpenReadStream();
        var saveResult = await _fileStorage.SaveImageAsync(stream, file.FileName, file.ContentType, "home-banners");
        if (saveResult.IsFailure)
        {
            TempData["Error"] = saveResult.FirstError.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }

        await _bannerService.SetImageAsync(id, saveResult.Value);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _bannerService.DeactivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _bannerService.ActivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _bannerService.DeleteAsync(id);
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
        await _bannerService.RestoreAsync(id);
        return RedirectToAction(nameof(Deleted));
    }
}
