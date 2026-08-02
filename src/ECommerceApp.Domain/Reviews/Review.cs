using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Reviews;

/// <summary>
/// One review per (user, product) - enforced via a unique index, the same
/// pattern WishlistItem's toggle constraint uses. AuditableEntity rather than
/// BaseEntity-only: unlike a wishlist bookmark or an immutable ledger row, a
/// review is a substantive piece of content that Milestone 12.2's moderation
/// will need to soft-delete without losing the audit trail. IsVerifiedPurchase
/// is computed once at submission time from the customer's order history (an
/// order whose payment actually succeeded - Paid/Shipped/Delivered/Cancelled,
/// the same "genuinely charged" reasoning Milestone 11's TotalSpent/invoice
/// eligibility already use) - a snapshot, not a live-recomputed flag.
/// </summary>
public class Review : AuditableEntity
{
    public int ProductId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsVerifiedPurchase { get; set; }

    public Product Product { get; set; } = null!;
}
