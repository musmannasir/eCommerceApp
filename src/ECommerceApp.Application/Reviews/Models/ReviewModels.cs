namespace ECommerceApp.Application.Reviews.Models;

public record CreateReviewRequest(int ProductId, int Rating, string? Title, string Body);

/// <summary>ReviewerDisplayName is "First name + last initial" (e.g. "Jane D.") - a reasonable
/// privacy-conscious default, not the account's full legal name.</summary>
public record ReviewDto(
    int Id,
    string ReviewerDisplayName,
    int Rating,
    string? Title,
    string Body,
    bool IsVerifiedPurchase,
    DateTime CreatedAtUtc);

/// <summary>RatingBreakdown always has keys 1-5 present (zero-filled), so a bar chart never
/// has to guard against a missing star level.</summary>
public record ProductRatingSummaryDto(decimal AverageRating, int ReviewCount, IReadOnlyDictionary<int, int> RatingBreakdown);
