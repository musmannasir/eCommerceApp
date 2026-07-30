namespace ECommerceApp.Domain.Orders;

/// <summary>
/// Deliberately just one value for now - Milestone 9.1 only creates an Order
/// once Checkout's existing validation succeeds, with nothing further having
/// happened to it yet. Payment outcomes (Milestone 9.2) and the fulfillment
/// state machine (Milestone 10.3) each add their own states when they exist;
/// adding them now would be speculative.
/// </summary>
public enum OrderStatus
{
    Pending,
}
