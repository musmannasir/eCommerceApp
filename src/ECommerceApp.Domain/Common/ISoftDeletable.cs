namespace ECommerceApp.Domain.Common;

/// <summary>
/// Implemented by entities that support soft deletion. Immutable financial
/// transaction records (payments, refunds, ledger entries, audit logs) must
/// NOT implement this interface — they are never soft-deleted or hard-deleted.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
