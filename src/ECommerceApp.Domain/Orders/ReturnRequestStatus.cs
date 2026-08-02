namespace ECommerceApp.Domain.Orders;

/// <summary>
/// Approved means staff have authorized the return and expect the item(s)
/// shipped back - it does not yet process a refund or restock inventory,
/// which only happens once the item is physically received back
/// (Milestone 13.3, "Refunds &amp; restocking").
/// </summary>
public enum ReturnRequestStatus
{
    Requested,
    Approved,
    Rejected,
}
