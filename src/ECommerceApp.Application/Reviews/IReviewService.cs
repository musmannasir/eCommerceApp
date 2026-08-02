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
}
