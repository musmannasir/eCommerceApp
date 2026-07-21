using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Storefront;

/// <summary>
/// One authenticated customer's most recent view of one product - a rolling,
/// pruned record (oldest rows past the configured max are deleted outright),
/// so it deliberately does not derive from AuditableEntity, same reasoning as
/// SupplierProduct/ProductTagMapping. UserId is a plain string, not a
/// navigation to ApplicationUser, matching RefreshToken/UserSession's pattern
/// (ApplicationUser lives in the Infrastructure layer, which Domain cannot
/// reference). Guests never reach the database - see IRecentlyViewedService.
/// </summary>
public class RecentlyViewedItem : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public DateTime ViewedAtUtc { get; set; }

    public Product Product { get; set; } = null!;
}
