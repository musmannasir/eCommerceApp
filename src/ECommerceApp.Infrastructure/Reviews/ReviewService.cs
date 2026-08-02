using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Reviews;
using ECommerceApp.Application.Reviews.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Orders;
using ECommerceApp.Domain.Reviews;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Reviews;

/// <summary>
/// Queries ApplicationDbContext directly, the same convention every other
/// Storefront-adjacent service follows. The rating summary is always
/// computed live from the Reviews table rather than denormalized onto
/// Product - matches this app's existing "compute at read time" posture for
/// stock aggregation and tax/shipping estimates, and avoids a cache-
/// invalidation problem for a feature with no measured scale need yet.
/// </summary>
public sealed class ReviewService : IReviewService
{
    private static readonly OrderStatus[] GenuinelyChargedStatuses =
    {
        OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Cancelled,
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;

    public ReviewService(ApplicationDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<ProductRatingSummaryDto> GetRatingSummaryAsync(int productId, CancellationToken cancellationToken = default)
    {
        var ratings = await _dbContext.Reviews
            .Where(r => r.ProductId == productId)
            .Select(r => r.Rating)
            .ToListAsync(cancellationToken);

        var breakdown = Enumerable.Range(1, 5).ToDictionary(star => star, star => ratings.Count(r => r == star));
        var average = ratings.Count > 0 ? Math.Round((decimal)ratings.Average(), 1) : 0m;

        return new ProductRatingSummaryDto(average, ratings.Count, breakdown);
    }

    public async Task<PagedResult<ReviewDto>> GetReviewsAsync(int productId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Reviews
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.Rating,
                r.Title,
                r.Body,
                r.IsVerifiedPurchase,
                r.CreatedAtUtc,
                Reviewer = _dbContext.Users.Where(u => u.Id == r.UserId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ReviewDto(
                r.Id,
                BuildDisplayName(r.Reviewer?.FirstName, r.Reviewer?.LastName),
                r.Rating,
                r.Title,
                r.Body,
                r.IsVerifiedPurchase,
                r.CreatedAtUtc))
            .ToList();

        return new PagedResult<ReviewDto>(items, totalCount, page, pageSize);
    }

    public Task<bool> HasReviewedAsync(string userId, int productId, CancellationToken cancellationToken = default) =>
        _dbContext.Reviews.AnyAsync(r => r.UserId == userId && r.ProductId == productId, cancellationToken);

    public async Task<Result<ReviewDto>> SubmitReviewAsync(string userId, CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive && p.IsPublished, cancellationToken);

        if (product is null)
        {
            return Result.Failure<ReviewDto>(Error.NotFound("review.product_not_found", "This product is not available."));
        }

        var alreadyReviewed = await _dbContext.Reviews
            .AnyAsync(r => r.UserId == userId && r.ProductId == request.ProductId, cancellationToken);

        if (alreadyReviewed)
        {
            return Result.Failure<ReviewDto>(Error.Conflict("review.already_reviewed", "You have already reviewed this product."));
        }

        var isVerifiedPurchase = await _dbContext.Orders
            .Where(o => o.UserId == userId && GenuinelyChargedStatuses.Contains(o.Status))
            .SelectMany(o => o.Items)
            .AnyAsync(i => i.ProductId == request.ProductId, cancellationToken);

        var review = new Review
        {
            ProductId = request.ProductId,
            UserId = userId,
            Rating = request.Rating,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title,
            Body = request.Body,
            IsVerifiedPurchase = isVerifiedPurchase,
        };

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var user = await _dbContext.Users.Where(u => u.Id == userId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken);

        return Result.Success(new ReviewDto(
            review.Id,
            BuildDisplayName(user?.FirstName, user?.LastName),
            review.Rating,
            review.Title,
            review.Body,
            review.IsVerifiedPurchase,
            review.CreatedAtUtc));
    }

    public async Task<Result> ReportReviewAsync(string reporterUserId, CreateReviewReportRequest request, CancellationToken cancellationToken = default)
    {
        var reviewExists = await _dbContext.Reviews.AnyAsync(r => r.Id == request.ReviewId, cancellationToken);
        if (!reviewExists)
        {
            return Result.Failure(Error.NotFound("review.not_found", "This review no longer exists."));
        }

        var alreadyReported = await _dbContext.ReviewReports
            .AnyAsync(r => r.ReviewId == request.ReviewId && r.ReporterUserId == reporterUserId, cancellationToken);

        if (alreadyReported)
        {
            return Result.Failure(Error.Conflict("review.already_reported", "You have already reported this review."));
        }

        _dbContext.ReviewReports.Add(new ReviewReport
        {
            ReviewId = request.ReviewId,
            ReporterUserId = reporterUserId,
            Reason = request.Reason,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment,
            CreatedAtUtc = _clock.UtcNow,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<PagedResult<ReviewModerationQueueItemDto>> GetModerationQueueAsync(ReviewModerationQuery query, CancellationToken cancellationToken = default)
    {
        var flagged = _dbContext.ReviewReports
            .GroupBy(r => r.ReviewId)
            .Select(g => new { ReviewId = g.Key, LatestReportAtUtc = g.Max(r => r.CreatedAtUtc), ReportCount = g.Count() });

        var totalCount = await flagged.CountAsync(cancellationToken);

        var page = await flagged
            .OrderByDescending(f => f.LatestReportAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var reviewIds = page.Select(p => p.ReviewId).ToList();

        var reviewRows = await _dbContext.Reviews
            .Where(rv => reviewIds.Contains(rv.Id))
            .Select(rv => new
            {
                rv.Id,
                rv.Rating,
                rv.Title,
                rv.Body,
                rv.CreatedAtUtc,
                ProductName = rv.Product.Name,
                ProductSlug = rv.Product.Slug,
                Reviewer = _dbContext.Users.Where(u => u.Id == rv.UserId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var reportRows = await _dbContext.ReviewReports
            .Where(r => reviewIds.Contains(r.ReviewId))
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new
            {
                r.ReviewId,
                r.Reason,
                r.Comment,
                r.CreatedAtUtc,
                Reporter = _dbContext.Users.Where(u => u.Id == r.ReporterUserId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var reportsByReview = reportRows
            .GroupBy(r => r.ReviewId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ReviewReportSummaryDto>)g
                    .Select(r => new ReviewReportSummaryDto(BuildDisplayName(r.Reporter?.FirstName, r.Reporter?.LastName), r.Reason, r.Comment, r.CreatedAtUtc))
                    .ToList());

        var items = page
            .Select(p =>
            {
                var review = reviewRows.First(r => r.Id == p.ReviewId);
                var reports = reportsByReview.TryGetValue(p.ReviewId, out var list) ? list : Array.Empty<ReviewReportSummaryDto>();
                return new ReviewModerationQueueItemDto(
                    review.Id, review.ProductName, review.ProductSlug,
                    BuildDisplayName(review.Reviewer?.FirstName, review.Reviewer?.LastName),
                    review.Rating, review.Title, review.Body, review.CreatedAtUtc,
                    p.ReportCount, reports);
            })
            .ToList();

        return new PagedResult<ReviewModerationQueueItemDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<Result> DismissReportsAsync(int reviewId, CancellationToken cancellationToken = default)
    {
        var reviewExists = await _dbContext.Reviews.AnyAsync(r => r.Id == reviewId, cancellationToken);
        if (!reviewExists)
        {
            return Result.Failure(Error.NotFound("review.not_found", "This review no longer exists."));
        }

        var reports = await _dbContext.ReviewReports.Where(r => r.ReviewId == reviewId).ToListAsync(cancellationToken);
        _dbContext.ReviewReports.RemoveRange(reports);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RemoveReviewAsync(int reviewId, CancellationToken cancellationToken = default)
    {
        var review = await _dbContext.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure(Error.NotFound("review.not_found", "This review no longer exists."));
        }

        var reports = await _dbContext.ReviewReports.Where(r => r.ReviewId == reviewId).ToListAsync(cancellationToken);
        _dbContext.ReviewReports.RemoveRange(reports);
        _dbContext.Reviews.Remove(review);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static string BuildDisplayName(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return "Anonymous";
        }

        return string.IsNullOrWhiteSpace(lastName) ? firstName : $"{firstName} {lastName[0]}.";
    }
}
