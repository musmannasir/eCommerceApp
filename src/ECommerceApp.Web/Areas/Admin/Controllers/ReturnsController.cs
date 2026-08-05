using ECommerceApp.Application.Returns;
using ECommerceApp.Application.Returns.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Models.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin return-request queues - requests still awaiting a decision
/// (Milestone 13.2), plus approved requests awaiting receipt/refund
/// (Milestone 13.3). Reuses Policies.CanManageOrders, the same choice the
/// review moderation queue (Milestone 12.2) made, rather than a new
/// dedicated role.
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
    public async Task<IActionResult> Index(int page = 1, int receivedPage = 1, CancellationToken cancellationToken = default)
    {
        var pending = await _returnService.GetPendingQueueAsync(new ReturnRequestQuery { Page = page }, cancellationToken);
        var awaitingReceipt = await _returnService.GetAwaitingReceiptQueueAsync(new ReturnRequestQuery { Page = receivedPage }, cancellationToken);
        return View(new ReturnsQueueViewModel(pending, awaitingReceipt));
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

    /// <summary>
    /// Money-moving, unlike Approve/Reject - narrowed to CanProcessRefunds
    /// (Milestone 14.1) so CustomerSupport, who can still triage
    /// Approve/Reject under the class-level CanManageOrders policy, cannot
    /// also trigger the refund itself. Stacked [Authorize] attributes
    /// combine with AND semantics, so this tightens rather than replaces
    /// the class-level policy.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.CanProcessRefunds)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(int id, CancellationToken cancellationToken)
    {
        var result = await _returnService.RefundAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Message" : "Error"] = result.IsSuccess
            ? "Item received - refund issued and stock restocked."
            : result.FirstError.Message;
        return RedirectToAction(nameof(Index));
    }
}
