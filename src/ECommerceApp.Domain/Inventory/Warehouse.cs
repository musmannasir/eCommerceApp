using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Inventory;

/// <summary>
/// One default warehouse is seeded for the app to work out of the box, but the
/// schema is multi-warehouse-capable from the start (Milestone 3 brief).
/// </summary>
public class Warehouse : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
}
