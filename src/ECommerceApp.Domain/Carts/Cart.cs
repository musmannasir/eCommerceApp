using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Marketing;

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

    /// <summary>
    /// The coupon/promotion currently applied to this cart (Milestone 7.1) -
    /// null if none. Re-validated on every read (CartService.BuildCartDtoAsync),
    /// not just at apply-time, since cart contents or the promotion itself can
    /// change afterward; an invalid one is silently cleared rather than left
    /// to produce a stale discount.
    /// </summary>
    public int? AppliedPromotionId { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    public Promotion? AppliedPromotion { get; set; }
}
