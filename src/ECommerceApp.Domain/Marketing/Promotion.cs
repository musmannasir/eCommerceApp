using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Marketing;

public enum PromotionDiscountType
{
    Percentage,
    FixedAmount,
}

/// <summary>Which part of the order a promotion's discount applies to - only the matching scope FK is set.</summary>
public enum PromotionScopeType
{
    EntireOrder,
    Category,
    Brand,
    Product,
}

/// <summary>
/// Admin-managed discount rule (Milestone 7.1) - either automatic (CouponCode
/// null, applies whenever a qualifying cart is evaluated) or code-based
/// (customer must enter CouponCode). MaxTotalUses/MaxUsesPerCustomer are
/// configurable but deliberately not enforced yet - there's no reliable
/// "this purchase actually completed" signal to count against until Order
/// entities exist (Milestone 9); enforcing against cart applications alone
/// would let an abandoned cart consume a limited code. See Architecture.md's
/// Milestone 7.1 section.
/// </summary>
public class Promotion : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CouponCode { get; set; }
    public PromotionDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }

    public PromotionScopeType ScopeType { get; set; }
    public int? ScopeCategoryId { get; set; }
    public int? ScopeBrandId { get; set; }
    public int? ScopeProductId { get; set; }

    public decimal? MinimumOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }

    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }

    public int? MaxTotalUses { get; set; }
    public int? MaxUsesPerCustomer { get; set; }

    public bool IsActive { get; set; } = true;

    public Category? ScopeCategory { get; set; }
    public Brand? ScopeBrand { get; set; }
    public Product? ScopeProduct { get; set; }
}
