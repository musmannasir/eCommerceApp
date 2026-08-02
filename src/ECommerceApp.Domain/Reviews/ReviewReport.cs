using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Reviews;

/// <summary>
/// One customer's flag on a review, driving the admin moderation queue
/// (Milestone 12.2) - BaseEntity, not AuditableEntity, since a report is a
/// one-time event that's never edited, the same reasoning WishlistItem uses
/// for its own toggle records. At most one report per (Review, reporter),
/// enforced via a unique index - mirrors the one-review-per-product
/// constraint Milestone 12.1's Review itself uses. Acting on the review
/// (Dismiss or Remove) clears its reports entirely rather than tracking a
/// resolved/unresolved status - there's no persistent moderation audit log
/// in this milestone's scope, so a review with reports is simply "still
/// queued" and one with none is not.
/// </summary>
public class ReviewReport : BaseEntity
{
    public int ReviewId { get; set; }
    public string ReporterUserId { get; set; } = string.Empty;
    public ReviewReportReason Reason { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Review Review { get; set; } = null!;
}
