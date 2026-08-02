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

    public ReviewService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
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

    private static string BuildDisplayName(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return "Anonymous";
        }

        return string.IsNullOrWhiteSpace(lastName) ? firstName : $"{firstName} {lastName[0]}.";
    }
}
