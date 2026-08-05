namespace ECommerceApp.Domain.Orders;

/// <summary>
/// Approved means staff have authorized the return and expect the item(s)
/// shipped back - it does not yet process a refund or restock inventory.
/// Refunded (Milestone 13.3) is the terminal state reached once the item is
/// physically received back - staff mark it received, which processes the
/// refund and restocks inventory in one step, rather than modeling a
/// separate "Received" state in between.
/// </summary>
public enum ReturnRequestStatus
{
    Requested,
    Approved,
    Rejected,
    Refunded,
}
