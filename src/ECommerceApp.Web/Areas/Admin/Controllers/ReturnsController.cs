using ECommerceApp.Application.Returns;
using ECommerceApp.Application.Returns.Models;
using ECommerceApp.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin return-request queue (Milestone 13.2) - every request still awaiting
/// a decision. Reuses Policies.CanManageOrders, the same choice the review
/// moderation queue (Milestone 12.2) made, rather than a new dedicated role.
/// </summary>
[Area("Admin")]
[Authorize(Policy = Policies.CanManageOrders)]
public class ReturnsController : Controller
{
    private readonly IReturnService _returnService;

    public ReturnsController(IReturnService returnService)
    {
        _returnService = returnService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _returnService.GetPendingQueueAsync(new ReturnRequestQuery { Page = page }, cancellationToken);
        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var result = await _returnService.ApproveAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Message" : "Error"] = result.IsSuccess ? "Return request approved." : result.FirstError.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string rejectionReason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            TempData["Error"] = "A rejection reason is required.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _returnService.RejectAsync(id, rejectionReason, cancellationToken);
        TempData[result.IsSuccess ? "Message" : "Error"] = result.IsSuccess ? "Return request rejected." : result.FirstError.Message;
        return RedirectToAction(nameof(Index));
    }
}
