using System.ComponentModel.DataAnnotations;
using ECommerceApp.Application.Inventory.Models;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class PurchaseOrderFormViewModel
{
    [Required, Display(Name = "Supplier")]
    public int SupplierId { get; set; }

    [Required, Display(Name = "Warehouse")]
    public int WarehouseId { get; set; }

    [Display(Name = "Expected delivery date")]
    [DataType(DataType.Date)]
    public DateTime? ExpectedDeliveryDate { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class AddPurchaseOrderItemViewModel
{
    public int PurchaseOrderId { get; set; }

    [Required, Display(Name = "Product")]
    public int ProductId { get; set; }

    [Required, Display(Name = "Quantity")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int QuantityOrdered { get; set; }

    [Required, Display(Name = "Unit cost")]
    [Range(0, double.MaxValue, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; set; }
}

public class PurchaseOrderEditViewModel
{
    public PurchaseOrderDto Order { get; set; } = null!;
    public IReadOnlyList<SupplierProductDto> LinkableProducts { get; set; } = Array.Empty<SupplierProductDto>();
    public IReadOnlyList<GoodsReceiptDto> Receipts { get; set; } = Array.Empty<GoodsReceiptDto>();
}

public class ReceiveGoodsLineViewModel
{
    public int PurchaseOrderItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Outstanding { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityReceived { get; set; }

    public bool AllowOverride { get; set; }
}

public class ReceiveGoodsViewModel
{
    public int PurchaseOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public List<ReceiveGoodsLineViewModel> Lines { get; set; } = new();

    [StringLength(500)]
    [Display(Name = "Override reason (required if receiving more than outstanding)")]
    public string? OverrideReason { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}
