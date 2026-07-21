using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Wishlist;

/// <summary>
/// One saved product per authenticated customer (Milestone 6.3) - product-level
/// only, no variant, a lighter bookmark than a cart line. Wishlist is
/// deliberately account-only, not guest-cookie-backed like Cart/RecentlyViewed:
/// it's meant to persist indefinitely and follow the customer across devices,
/// which a cookie can't do, and it's a lower-frequency action than adding to
/// cart where guest friction actually matters.
/// </summary>
public class WishlistItem : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public DateTime AddedAtUtc { get; set; }

    public Product Product { get; set; } = null!;
}
