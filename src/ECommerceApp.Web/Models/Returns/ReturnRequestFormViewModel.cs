using System.ComponentModel.DataAnnotations;
using ECommerceApp.Domain.Orders;

namespace ECommerceApp.Web.Models.Returns;

public class ReturnRequestFormViewModel
{
    [Required]
    public ReturnReason Reason { get; set; }

    [StringLength(1000), Display(Name = "Additional details (optional)")]
    public string? Comment { get; set; }

    /// <summary>One entry per order line - Quantity 0 means "not returned"; only lines with Quantity &gt; 0 are submitted.</summary>
    public List<ReturnRequestFormItemViewModel> Items { get; set; } = new();
}

public class ReturnRequestFormItemViewModel
{
    public int OrderItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int OrderedQuantity { get; set; }
    public int Quantity { get; set; }
}
