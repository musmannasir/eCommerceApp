namespace ECommerceApp.Domain.Inventory;

public enum PurchaseOrderStatus
{
    Draft,
    Submitted,
    Approved,
    PartiallyReceived,
    Received,
    Cancelled,
}
