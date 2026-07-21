using ECommerceApp.Application.Catalog;
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
public class SuppliersController : Controller
{
    private readonly ISupplierService _supplierService;
    private readonly IProductService _productService;

    public SuppliersController(ISupplierService supplierService, IProductService productService)
    {
        _supplierService = supplierService;
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? sortBy, bool sortDescending = false, int page = 1)
    {
        var result = await _supplierService.GetPagedAsync(new PagedQuery
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
        var result = await _supplierService.GetPagedAsync(new PagedQuery { Page = page, PageSize = 20, OnlyDeleted = true });
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create() => View(new SupplierFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupplierFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _supplierService.CreateAsync(new CreateSupplierRequest(
            model.Name, model.Code, model.ContactName, model.Email, model.Phone,
            model.AddressLine1, model.AddressLine2, model.City, model.Region, model.PostalCode, model.Country,
            model.Website, model.Notes, model.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = $"Supplier '{result.Value.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _supplierService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var supplier = result.Value;
        var form = new SupplierFormViewModel
        {
            Id = supplier.Id,
            Name = supplier.Name,
            Code = supplier.Code,
            ContactName = supplier.ContactName,
            Email = supplier.Email,
            Phone = supplier.Phone,
            AddressLine1 = supplier.AddressLine1,
            AddressLine2 = supplier.AddressLine2,
            City = supplier.City,
            Region = supplier.Region,
            PostalCode = supplier.PostalCode,
            Country = supplier.Country,
            Website = supplier.Website,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive,
        };

        return View(await BuildEditViewModelAsync(form));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SupplierFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildEditViewModelAsync(form));
        }

        var result = await _supplierService.UpdateAsync(new UpdateSupplierRequest(
            form.Id, form.Name, form.Code, form.ContactName, form.Email, form.Phone,
            form.AddressLine1, form.AddressLine2, form.City, form.Region, form.PostalCode, form.Country,
            form.Website, form.Notes, form.IsActive));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(await BuildEditViewModelAsync(form));
        }

        TempData["Message"] = $"Supplier '{result.Value.Name}' updated.";
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    private async Task<SupplierEditViewModel> BuildEditViewModelAsync(SupplierFormViewModel form)
    {
        var linkedProducts = await _supplierService.GetLinkedProductsAsync(form.Id);
        var productPicker = await _productService.GetPickerListAsync();
        var linkedProductIds = linkedProducts.Value.Select(l => l.ProductId).ToHashSet();

        return new SupplierEditViewModel
        {
            Form = form,
            LinkedProducts = linkedProducts.Value,
            AvailableProducts = productPicker.Value.Where(p => !linkedProductIds.Contains(p.Id)).ToList(),
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkProduct(LinkSupplierProductViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _supplierService.LinkProductAsync(new LinkSupplierProductRequest(
                model.SupplierId, model.ProductId, model.SupplierSku, model.CostPrice, model.LeadTimeDays, model.IsPreferred));

            if (result.IsFailure)
            {
                TempData["Error"] = result.FirstError.Message;
            }
        }

        return RedirectToAction(nameof(Edit), new { id = model.SupplierId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkProduct(int supplierProductId, int supplierId)
    {
        await _supplierService.UnlinkProductAsync(supplierProductId);
        return RedirectToAction(nameof(Edit), new { id = supplierId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _supplierService.DeactivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _supplierService.ActivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _supplierService.DeleteAsync(id);
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
        await _supplierService.RestoreAsync(id);
        return RedirectToAction(nameof(Deleted));
    }
}
