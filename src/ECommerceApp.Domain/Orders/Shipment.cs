using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Orders;

/// <summary>
/// One shipment per order (Milestone 10.3) - a v1 scope choice, same as
/// Payment's "one charge per order": nothing upstream splits an order into
/// multiple packages, so a real multi-shipment model would be speculative.
/// Has a real mutable lifecycle (shipped -> delivered), the same reasoning
/// <see cref="Inventory.InventoryReservation"/> uses for deriving from
/// <see cref="AuditableEntity"/> instead of an immutable, insert-once type
/// like <see cref="Payments.Payment"/> or <see cref="Inventory.StockMovement"/>.
/// </summary>
public class Shipment : AuditableEntity
{
    public int OrderId { get; set; }
    public string Carrier { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public DateTime ShippedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }

    public Order Order { get; set; } = null!;
}
