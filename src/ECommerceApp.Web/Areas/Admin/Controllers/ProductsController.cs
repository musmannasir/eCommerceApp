using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageCatalog)]
public class ProductsController : Controller
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IBrandService _brandService;
    private readonly IProductAttributeService _attributeService;

    public ProductsController(
        IProductService productService,
        ICategoryService categoryService,
        IBrandService brandService,
        IProductAttributeService attributeService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _brandService = brandService;
        _attributeService = attributeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? categoryId, int? brandId, string? sortBy, bool sortDescending = false, int page = 1)
    {
        var result = await _productService.GetPagedAsync(new ProductListQuery
        {
            Page = page,
            PageSize = 20,
            Search = search,
            CategoryId = categoryId,
            BrandId = brandId,
            SortBy = sortBy,
            SortDescending = sortDescending,
        });

        ViewData["Search"] = search;
        ViewData["Categories"] = (await _categoryService.GetAllActiveAsync()).Value;
        ViewData["Brands"] = (await _brandService.GetAllActiveAsync()).Value;
        ViewData["SelectedCategoryId"] = categoryId;
        ViewData["SelectedBrandId"] = brandId;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Deleted(int page = 1)
    {
        var result = await _productService.GetPagedAsync(new ProductListQuery { Page = page, PageSize = 20, OnlyDeleted = true });
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ProductFormViewModel
        {
            AvailableCategories = (await _categoryService.GetAllActiveAsync()).Value,
            AvailableBrands = (await _brandService.GetAllActiveAsync()).Value,
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableCategories = (await _categoryService.GetAllActiveAsync()).Value;
            model.AvailableBrands = (await _brandService.GetAllActiveAsync()).Value;
            return View(model);
        }

        var result = await _productService.CreateAsync(new CreateProductRequest(
            model.Name, model.Slug, model.ShortDescription, model.FullDescription, model.BrandId, model.CategoryId,
            model.BaseSKU, model.CostPrice, model.SellingPrice, model.CompareAtPrice, model.TaxCategory, model.IsTaxable,
            model.IsActive, model.IsFeatured, model.Weight, model.Length, model.Width, model.Height,
            model.WarrantyInformation, model.ReturnEligibility, model.LowStockThreshold, model.SearchKeywords,
            model.MetaTitle, model.MetaDescription));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            model.AvailableCategories = (await _categoryService.GetAllActiveAsync()).Value;
            model.AvailableBrands = (await _brandService.GetAllActiveAsync()).Value;
            return View(model);
        }

        TempData["Message"] = $"Product '{result.Value.Name}' created. Add images, variants, specifications, and tags below.";
        return RedirectToAction(nameof(Edit), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var productResult = await _productService.GetByIdAsync(id);
        if (productResult.IsFailure)
        {
            return NotFound();
        }

        var product = productResult.Value;
        var model = new ProductEditViewModel
        {
            Product = product,
            Attributes = (await _attributeService.GetAllAsync()).Value,
            Form = new ProductFormViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Slug = product.Slug,
                ShortDescription = product.ShortDescription,
                FullDescription = product.FullDescription,
                BrandId = product.BrandId,
                CategoryId = product.CategoryId,
                BaseSKU = product.BaseSKU,
                CostPrice = product.CostPrice,
                SellingPrice = product.SellingPrice,
                CompareAtPrice = product.CompareAtPrice,
                TaxCategory = product.TaxCategory,
                IsTaxable = product.IsTaxable,
                IsActive = product.IsActive,
                IsFeatured = product.IsFeatured,
                Weight = product.Weight,
                Length = product.Length,
                Width = product.Width,
                Height = product.Height,
                WarrantyInformation = product.WarrantyInformation,
                ReturnEligibility = product.ReturnEligibility,
                LowStockThreshold = product.LowStockThreshold,
                SearchKeywords = product.SearchKeywords,
                MetaTitle = product.MetaTitle,
                MetaDescription = product.MetaDescription,
                AvailableCategories = (await _categoryService.GetAllActiveAsync()).Value,
                AvailableBrands = (await _brandService.GetAllActiveAsync()).Value,
            },
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnEditWithErrorsAsync(form);
        }

        var result = await _productService.UpdateAsync(new UpdateProductRequest(
            form.Id, form.Name, form.Slug, form.ShortDescription, form.FullDescription, form.BrandId, form.CategoryId,
            form.BaseSKU, form.CostPrice, form.SellingPrice, form.CompareAtPrice, form.TaxCategory, form.IsTaxable,
            form.IsActive, form.IsFeatured, form.Weight, form.Length, form.Width, form.Height,
            form.WarrantyInformation, form.ReturnEligibility, form.LowStockThreshold, form.SearchKeywords,
            form.MetaTitle, form.MetaDescription));

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return await ReturnEditWithErrorsAsync(form);
        }

        TempData["Message"] = "Product updated.";
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var result = await _productService.PublishAsync(id);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(int id)
    {
        await _productService.UnpublishAsync(id);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _productService.DeactivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _productService.ActivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _productService.DeleteAsync(id);
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
        await _productService.RestoreAsync(id);
        return RedirectToAction(nameof(Deleted));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVariant(AddVariantViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _productService.AddVariantAsync(new CreateVariantRequest(
                model.ProductId, model.SKU, model.Barcode, model.CostPrice, model.SellingPrice,
                model.CompareAtPrice, model.Weight, model.IsActive, model.AttributeValueIds));

            if (result.IsFailure)
            {
                TempData["Error"] = result.FirstError.Message;
            }
        }
        else
        {
            TempData["Error"] = "Please correct the variant form and try again.";
        }

        return RedirectToAction(nameof(Edit), new { id = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVariant(int variantId, int productId)
    {
        await _productService.DeleteVariantAsync(variantId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSpecification(AddSpecificationViewModel model)
    {
        if (ModelState.IsValid)
        {
            await _productService.AddSpecificationAsync(new CreateSpecificationRequest(model.ProductId, model.Name, model.Value, model.DisplayOrder));
        }

        return RedirectToAction(nameof(Edit), new { id = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSpecification(int specificationId, int productId)
    {
        await _productService.DeleteSpecificationAsync(specificationId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTag(AddTagViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _productService.AddTagAsync(model.ProductId, model.TagName);
            if (result.IsFailure)
            {
                TempData["Error"] = result.FirstError.Message;
            }
        }

        return RedirectToAction(nameof(Edit), new { id = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTag(int productId, int productTagId)
    {
        await _productService.RemoveTagAsync(productId, productTagId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddImage(int productId, int? productVariantId, IFormFile file, string? altText, bool isPrimary)
    {
        if (file is { Length: > 0 })
        {
            await using var stream = file.OpenReadStream();
            var result = await _productService.AddImageAsync(productId, productVariantId, stream, file.FileName, file.ContentType, altText, isPrimary);
            if (result.IsFailure)
            {
                TempData["Error"] = result.FirstError.Message;
            }
        }
        else
        {
            TempData["Error"] = "Please choose a file to upload.";
        }

        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int imageId, int productId)
    {
        await _productService.DeleteImageAsync(imageId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    private async Task<IActionResult> ReturnEditWithErrorsAsync(ProductFormViewModel form)
    {
        var productResult = await _productService.GetByIdAsync(form.Id);
        form.AvailableCategories = (await _categoryService.GetAllActiveAsync()).Value;
        form.AvailableBrands = (await _brandService.GetAllActiveAsync()).Value;

        var model = new ProductEditViewModel
        {
            Form = form,
            Product = productResult.Value,
            Attributes = (await _attributeService.GetAllAsync()).Value,
        };
        return View(nameof(Edit), model);
    }
}
