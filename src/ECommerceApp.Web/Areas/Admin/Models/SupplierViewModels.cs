using System.ComponentModel.DataAnnotations;
using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Inventory.Models;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class SupplierEditViewModel
{
    public SupplierFormViewModel Form { get; set; } = new();
    public IReadOnlyList<SupplierProductDto> LinkedProducts { get; set; } = Array.Empty<SupplierProductDto>();
    public IReadOnlyList<ProductPickerItemDto> AvailableProducts { get; set; } = Array.Empty<ProductPickerItemDto>();
}

public class SupplierFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Contact name"), StringLength(200)]
    public string? ContactName { get; set; }

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [Phone, StringLength(50)]
    public string? Phone { get; set; }

    [Display(Name = "Address line 1"), StringLength(200)]
    public string? AddressLine1 { get; set; }

    [Display(Name = "Address line 2"), StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? Region { get; set; }

    [Display(Name = "Postal code"), StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [Url, StringLength(500)]
    public string? Website { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class LinkSupplierProductViewModel
{
    public int SupplierId { get; set; }

    [Required, Display(Name = "Product")]
    public int ProductId { get; set; }

    [Display(Name = "Supplier SKU"), StringLength(100)]
    public string? SupplierSku { get; set; }

    [Display(Name = "Cost price")]
    [Range(0, double.MaxValue, ErrorMessage = "Cost price cannot be negative.")]
    public decimal? CostPrice { get; set; }

    [Display(Name = "Lead time (days)")]
    [Range(0, int.MaxValue, ErrorMessage = "Lead time cannot be negative.")]
    public int? LeadTimeDays { get; set; }

    [Display(Name = "Preferred supplier")]
    public bool IsPreferred { get; set; }
}
