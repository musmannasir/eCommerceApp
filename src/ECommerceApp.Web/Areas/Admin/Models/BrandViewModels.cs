using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class BrandFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Slug { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Url, StringLength(300)]
    public string? Website { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Featured")]
    public bool IsFeatured { get; set; }

    public string? LogoPath { get; set; }
}
