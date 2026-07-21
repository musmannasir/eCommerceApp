using System.Security.Claims;
using ECommerceApp.Application.Storefront;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers;

/// <summary>
/// Public product detail page. Details() does reload-based variant resolution
/// (query string -> server resolves -> full page render), lenient toward an
/// arbitrary/bookmarked URL. Resolve() backs the live, no-reload variant
/// switcher (Milestone 5.2) - strict, server-authoritative, and the only
/// path a customer's actual in-page interaction ever takes.
/// </summary>
public class ProductController : Controller
{
    private readonly IProductDetailService _productDetailService;
    private readonly IRecentlyViewedService _recentlyViewedService;

    public ProductController(IProductDetailService productDetailService, IRecentlyViewedService recentlyViewedService)
    {
        _productDetailService = productDetailService;
        _recentlyViewedService = recentlyViewedService;
    }

    [HttpGet("Product/{slug}")]
    public async Task<IActionResult> Details(string slug, int? variantId, int[] attr)
    {
        var userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        var result = await _productDetailService.GetDetailAsync(slug, variantId, attr, userId);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var product = result.Value;
        ViewData["Breadcrumbs"] = product.Breadcrumbs
            .Select(b => (b.Text, b.Url))
            .ToList();

        await _recentlyViewedService.RecordViewAsync(product.Id);

        return View(product);
    }

    [HttpGet("Product/{slug}/Resolve")]
    public async Task<IActionResult> Resolve(string slug, int variantId)
    {
        var result = await _productDetailService.ResolveVariantAsync(slug, variantId);
        if (result.IsFailure)
        {
            return NotFound(new { message = result.FirstError.Message });
        }

        return Json(result.Value);
    }
}
