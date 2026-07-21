using System.ComponentModel.DataAnnotations;
using ECommerceApp.Application.Inventory.Models;

namespace ECommerceApp.Web.Areas.Admin.Models;

public record ProductVariantPickerDto(int Id, string SKU);

public record ProductPickerDto(int Id, string Name, string BaseSKU, IReadOnlyList<ProductVariantPickerDto> Variants);

public class OpeningStockFormViewModel
{
    [Display(Name = "Warehouse")]
    public int WarehouseId { get; set; }

    [Display(Name = "Product")]
    public int ProductId { get; set; }

    [Display(Name = "Variant")]
    public int? ProductVariantId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
    public int Quantity { get; set; }

    [Display(Name = "Reorder level"), Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }

    [Display(Name = "Reorder quantity"), Range(0, int.MaxValue)]
    public int ReorderQuantity { get; set; }

    [Display(Name = "Allow backorder")]
    public bool AllowBackorder { get; set; }

    public IEnumerable<WarehouseDto> AvailableWarehouses { get; set; } = [];
    public IEnumerable<ProductPickerDto> AvailableProducts { get; set; } = [];
}

public class AdjustStockFormViewModel
{
    public int InventoryItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public int CurrentQuantityOnHand { get; set; }
    public int CurrentQuantityAvailable { get; set; }

    public int QuantityDelta { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}
