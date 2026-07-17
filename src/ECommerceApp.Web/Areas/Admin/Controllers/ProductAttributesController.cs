using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Policies.CanManageCatalog)]
public class ProductAttributesController : Controller
{
    private readonly IProductAttributeService _attributeService;

    public ProductAttributesController(IProductAttributeService attributeService)
    {
        _attributeService = attributeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _attributeService.GetAllAsync();
        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAttribute(CreateAttributeViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _attributeService.CreateAttributeAsync(new CreateProductAttributeRequest(model.Name));
            if (result.IsFailure)
            {
                TempData["Error"] = result.FirstError.Message;
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateValue(CreateAttributeValueViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _attributeService.CreateValueAsync(new CreateProductAttributeValueRequest(model.ProductAttributeId, model.Value));
            if (result.IsFailure)
            {
                TempData["Error"] = result.FirstError.Message;
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
