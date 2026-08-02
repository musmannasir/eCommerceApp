using System.Security.Claims;
using ECommerceApp.Application.Reviews;
using ECommerceApp.Application.Reviews.Models;
using ECommerceApp.Application.Storefront;
using ECommerceApp.Web.Models.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers;

/// <summary>
/// Public product detail page. Details() does reload-based variant resolution
/// (query string -> server resolves -> full page render), lenient toward an
/// arbitrary/bookmarked URL. Resolve() backs the live, no-reload variant
/// switcher (Milestone 5.2) - strict, server-authoritative, and the only
/// path a customer's actual in-page interaction ever takes. SubmitReview()
/// (Milestone 12.1) is a classic form POST, not AJAX, unlike Wishlist's
/// toggle - a review is substantive content worth a full page reload and a
/// TempData-surfaced outcome, not a quiet background call.
/// </summary>
public class ProductController : Controller
{
    private readonly IProductDetailService _productDetailService;
    private readonly IRecentlyViewedService _recentlyViewedService;
    private readonly IReviewService _reviewService;

    public ProductController(
        IProductDetailService productDetailService, IRecentlyViewedService recentlyViewedService, IReviewService reviewService)
    {
        _productDetailService = productDetailService;
        _recentlyViewedService = recentlyViewedService;
        _reviewService = reviewService;
    }

    [HttpGet("Product/{slug}")]
    public async Task<IActionResult> Details(string slug, int? variantId, int[] attr, int reviewsPage = 1)
    {
        var userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        var result = await _productDetailService.GetDetailAsync(slug, variantId, attr, userId, reviewsPage);
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

    [Authorize]
    [HttpPost("Product/{slug}/Review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(string slug, ReviewFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ReviewError"] = "Please provide a rating and a review before submitting.";
            return RedirectToAction(nameof(Details), new { slug });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var request = new CreateReviewRequest(model.ProductId, model.Rating, model.Title, model.Body);
        var result = await _reviewService.SubmitReviewAsync(userId, request, cancellationToken);

        TempData[result.IsSuccess ? "ReviewMessage" : "ReviewError"] =
            result.IsSuccess ? "Thanks - your review has been posted." : result.FirstError.Message;

        return RedirectToAction(nameof(Details), new { slug });
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
