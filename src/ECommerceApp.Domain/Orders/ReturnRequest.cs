using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Orders;

/// <summary>
/// A customer's request to return some or all of a Delivered order's items
/// (Milestone 13.1). UserId is a snapshot (mirrors Review's own UserId
/// denormalization), not just derivable via Order.UserId, so ownership
/// queries don't need a join. At most one open (Requested/Approved) request
/// per order is enforced at the service layer via check-then-insert - the
/// same pattern Review/ReviewReport already use - not a DB-level filtered
/// index. Only a Delivered order is eligible; there is no day-count
/// return window anywhere in this app to enforce (Product.ReturnEligibility
/// is deliberately unstructured free text), so eligibility is gated purely
/// by order status.
/// </summary>
public class ReturnRequest : AuditableEntity
{
    public int OrderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ReturnReason Reason { get; set; }
    public string? Comment { get; set; }
    public ReturnRequestStatus Status { get; set; } = ReturnRequestStatus.Requested;

    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedByUserId { get; set; }
    public string? RejectionReason { get; set; }

    public Order Order { get; set; } = null!;
    public ICollection<ReturnRequestItem> Items { get; set; } = new List<ReturnRequestItem>();
}
