using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Inventory;

/// <summary>Plain join record linking a supplier to a product it can source - unlinking removes the row outright, no soft delete.</summary>
public class SupplierProduct : BaseEntity
{
    public int SupplierId { get; set; }
    public int ProductId { get; set; }
    public string? SupplierSku { get; set; }
    public decimal? CostPrice { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsPreferred { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
