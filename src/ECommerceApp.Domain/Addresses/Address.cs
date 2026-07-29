using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Addresses;

/// <summary>
/// A customer's saved address (Milestone 8.1) - reused for both shipping and
/// billing at checkout (Milestone 8.2), not split into separate entity types,
/// since a v1 address book doesn't need that distinction. CountryCode/RegionCode
/// mirror TaxRate/ShippingMethod's shape exactly, so a real destination can be
/// passed straight into ICheckoutCalculationService.CalculateAsync (Milestone
/// 7.4) once real checkout exists. Account-only, like Wishlist - no guest
/// concept - and uses plain BaseEntity (no soft delete/RowVersion), the same
/// convention Cart/CartItem/WishlistItem already established for customer-owned
/// personal records: a user who deletes their own address wants it gone, and
/// there's no admin recycle bin for personal data.
/// </summary>
public class Address : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? RegionCode { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// At most one default per user, enforced by AddressService (not a DB
    /// constraint) - setting a new default clears the flag from whichever
    /// address had it before, in the same transaction.
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
