using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class HomePageBannerFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Subtitle { get; set; }

    [StringLength(500)]
    public string? LinkUrl { get; set; }

    [Required, Display(Name = "Banner type")]
    public string BannerType { get; set; } = "Hero";

    [Display(Name = "Display order")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public string? ImagePath { get; set; }
}
