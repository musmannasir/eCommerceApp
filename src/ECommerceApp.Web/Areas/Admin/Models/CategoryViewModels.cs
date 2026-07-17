using System.ComponentModel.DataAnnotations;
using ECommerceApp.Application.Catalog.Models;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class CategoryFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Slug { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "Parent category")]
    public int? ParentCategoryId { get; set; }

    [Display(Name = "Display order")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Featured")]
    public bool IsFeatured { get; set; }

    public IEnumerable<CategoryDto> AvailableParents { get; set; } = [];
}
