using ECommerceApp.Domain.Reviews;

namespace ECommerceApp.Application.Reviews.Models;

public record CreateReviewReportRequest(int ReviewId, ReviewReportReason Reason, string? Comment);

/// <summary>ReporterDisplayName follows ReviewDto's own "first name + last initial" convention.</summary>
public record ReviewReportSummaryDto(string ReporterDisplayName, ReviewReportReason Reason, string? Comment, DateTime CreatedAtUtc);

/// <summary>One row in the admin moderation queue - a review that currently has at least one open report.</summary>
public record ReviewModerationQueueItemDto(
    int ReviewId,
    string ProductName,
    string ProductSlug,
    string ReviewerDisplayName,
    int Rating,
    string? Title,
    string Body,
    DateTime CreatedAtUtc,
    int ReportCount,
    IReadOnlyList<ReviewReportSummaryDto> Reports);

public record ReviewModerationQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
