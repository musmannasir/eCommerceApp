using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Orders;

namespace ECommerceApp.Domain.Payments;

/// <summary>
/// A refund issued once a return request's items are physically received
/// back (Milestone 13.3). Deliberately does NOT derive from
/// <see cref="AuditableEntity"/>, the same reasoning <see cref="Payment"/>
/// itself uses: an immutable, insert-once financial ledger entry. Order.Payment
/// is a single reference, not a collection, so a refund is recorded here as a
/// new, separate transaction rather than by editing the original Payment row -
/// exactly what Payment's own remarks said Milestone 13.3 would do.
/// ReturnRequestStatus.Refunded is the only route to creating one, so a
/// second refund attempt is already rejected by that status check before this
/// row would ever be duplicated; the unique index on ReturnRequestId is
/// defense in depth.
/// </summary>
public class Refund : BaseEntity
{
    public int OrderId { get; set; }
    public int ReturnRequestId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
    public string? ProcessedByUserId { get; set; }

    public Order Order { get; set; } = null!;
    public ReturnRequest ReturnRequest { get; set; } = null!;
}
