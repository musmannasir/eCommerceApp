using ECommerceApp.Application.Reviews;
using ECommerceApp.Application.Reviews.Models;
using ECommerceApp.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin review-moderation queue (Milestone 12.2) - every review that
/// currently has at least one open report. Reuses Policies.CanManageOrders
/// (already grants CustomerSupport) rather than a new dedicated policy/role,
/// since no "Moderator" role exists anywhere else in this app and inventing
/// one for a single screen would be speculative.
/// </summary>
[Area("Admin")]
[Authorize(Policy = Policies.CanManageOrders)]
public class ReviewsController : Controller
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var result = await _reviewService.GetModerationQueueAsync(new ReviewModerationQuery { Page = page });
        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int reviewId)
    {
        var result = await _reviewService.DismissReportsAsync(reviewId);
        TempData[result.IsSuccess ? "Message" : "Error"] = result.IsSuccess ? "Report(s) dismissed - the review stays live." : result.FirstError.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int reviewId)
    {
        var result = await _reviewService.RemoveReviewAsync(reviewId);
        TempData[result.IsSuccess ? "Message" : "Error"] = result.IsSuccess ? "Review removed." : result.FirstError.Message;
        return RedirectToAction(nameof(Index));
    }
}
