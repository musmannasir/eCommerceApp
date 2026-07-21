using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class WarehouseFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

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

    [Display(Name = "Default warehouse")]
    public bool IsDefault { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
