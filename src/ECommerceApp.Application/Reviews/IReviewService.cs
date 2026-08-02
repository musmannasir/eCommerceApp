using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Reviews.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Reviews;

public interface IReviewService
{
    Task<ProductRatingSummaryDto> GetRatingSummaryAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Newest first, the same default every other listing in this app uses.</summary>
    Task<PagedResult<ReviewDto>> GetReviewsAsync(int productId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<bool> HasReviewedAsync(string userId, int productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Any authenticated customer may review any product regardless of purchase
    /// history - IsVerifiedPurchase (computed here from order history) is a badge,
    /// not a gate. Fails with Conflict if the user has already reviewed this
    /// product, or NotFound if the product doesn't exist/isn't published.
    /// </summary>
    Task<Result<ReviewDto>> SubmitReviewAsync(string userId, CreateReviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fails with Conflict if this reporter has already reported this review, or
    /// NotFound if the review doesn't exist. No self-report guard - a customer
    /// reporting their own review is harmless, low-signal noise for a moderator
    /// to dismiss, not a case worth blocking.
    /// </summary>
    Task<Result> ReportReviewAsync(string reporterUserId, CreateReviewReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>Admin moderation queue (Milestone 12.2) - every review that currently has at least one open report, newest-reported first.</summary>
    Task<PagedResult<ReviewModerationQueueItemDto>> GetModerationQueueAsync(ReviewModerationQuery query, CancellationToken cancellationToken = default);

    /// <summary>Clears every report on this review without removing it - the review stays live.</summary>
    Task<Result> DismissReportsAsync(int reviewId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the review (excluding it from every read via the existing global query filter) and clears its reports.</summary>
    Task<Result> RemoveReviewAsync(int reviewId, CancellationToken cancellationToken = default);
}
