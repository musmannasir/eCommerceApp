using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Marketing;

/// <summary>
/// Admin-managed home page content (Milestone 4.1 brief: hero banners and
/// promo blocks must be admin-managed, not hardcoded). LinkUrl is free-form
/// and admin-supplied, unlike the system-generated category/product links
/// on the home page - which stay non-clickable until Milestones 4.2/5 build
/// their destination pages.
/// </summary>
public class HomePageBanner : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? ImagePath { get; set; }
    public string? LinkUrl { get; set; }
    public BannerType BannerType { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
