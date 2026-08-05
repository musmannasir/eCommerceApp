using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Orders;

namespace ECommerceApp.Domain.Payments;

/// <summary>
/// The result of a single (simulated) charge attempt against an Order -
/// Milestone 9.2. Deliberately does NOT derive from <see cref="AuditableEntity"/>
/// (no soft delete, no UpdatedAt, no RowVersion), the same reasoning
/// <see cref="Inventory.StockMovement"/> uses: this row is written once,
/// synchronously, with its final outcome already known, and never updated
/// or deleted afterward - a correction (a refund, Milestone 13.3) records a
/// new, separate <see cref="Refund"/> transaction rather than editing this
/// one. <see cref="ISoftDeletable"/>'s own doc comment is explicit that
/// "immutable financial transaction records (payments, refunds, ledger
/// entries, audit logs) must NOT implement this interface."
/// Never stores the real card number - only a masked last-4 and the
/// detected brand, mirroring real PCI-compliant practice even in
/// simulation.
/// </summary>
public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public PaymentMethodType MethodType { get; set; } = PaymentMethodType.CreditCard;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public string MaskedCardNumber { get; set; } = string.Empty;
    public string CardBrand { get; set; } = string.Empty;
    public string? DeclineReason { get; set; }
    public DateTime ProcessedAtUtc { get; set; }

    public Order Order { get; set; } = null!;
}
