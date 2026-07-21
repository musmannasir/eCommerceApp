using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Carts;

/// <summary>
/// One line item for a product, or for one specific variant of a product -
/// never both at once for the same product in the same cart (enforced by two
/// filtered unique indexes, the same pattern InventoryItem already uses for
/// "one purchasable unit per warehouse"). LineTotal is always computed from
/// the *live* price via IPricingService, never from PriceWhenAdded - that
/// field exists purely so CartService can tell the customer their price
/// changed since they added it (Milestone 6.2), not to charge a stale amount.
/// </summary>
public class CartItem : BaseEntity
{
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public decimal PriceWhenAdded { get; set; }
    public DateTime AddedAtUtc { get; set; }

    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
}
