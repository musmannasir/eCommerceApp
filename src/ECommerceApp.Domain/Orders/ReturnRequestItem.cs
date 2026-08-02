using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Orders;

/// <summary>AuditableEntity, mirroring OrderItem/PurchaseOrderItem's own base-type choice for a mutable, auditable order line.</summary>
public class ReturnRequestItem : AuditableEntity
{
    public int ReturnRequestId { get; set; }
    public int OrderItemId { get; set; }
    public int Quantity { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
