using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Carts;

/// <summary>
/// Exactly one of <see cref="UserId"/> or <see cref="GuestToken"/> is set, never
/// both and never neither - enforced by two filtered unique indexes in the
/// Infrastructure layer's EF Core configuration, and by CartService only ever
/// constructing a Cart through its single owner-resolving factory path. A guest
/// cart is created lazily on first add, keyed by a token issued via a cookie
/// (see the Web project's ICartOwnerAccessor) - not every anonymous visitor gets
/// a row, only one who actually adds something.
/// </summary>
public class Cart : BaseEntity
{
    public string? UserId { get; set; }
    public string? GuestToken { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
