using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Addresses;

/// <summary>
/// Customer address book (Milestone 8.1) - account-only, like Wishlist, since
/// an address is meant to persist across visits/devices the same way a
/// wishlist is. Every operation is scoped to the owning user; an id that
/// exists but belongs to a different user returns NotFound (same as an id
/// that doesn't exist at all), never Forbidden, so existence isn't leaked
/// across accounts.
/// </summary>
public interface IAddressService
{
    /// <summary>Most-recently-updated first, matching the Wishlist/RecentlyViewed ordering convention.</summary>
    Task<IReadOnlyList<AddressDto>> GetAddressesAsync(string userId, CancellationToken cancellationToken = default);

    Task<Result<AddressDto>> GetByIdAsync(string userId, int id, CancellationToken cancellationToken = default);

    /// <summary>The first address a user ever saves is automatically made the default, regardless of the request's IsDefault flag.</summary>
    Task<Result<AddressDto>> CreateAsync(string userId, CreateAddressRequest request, CancellationToken cancellationToken = default);

    Task<Result<AddressDto>> UpdateAsync(string userId, UpdateAddressRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deleting the current default leaves no default at all - the customer
    /// picks a new one explicitly via SetDefaultAsync rather than one being
    /// silently promoted for them.
    /// </summary>
    Task<Result> DeleteAsync(string userId, int id, CancellationToken cancellationToken = default);

    /// <summary>Marks this address as the default, clearing the flag from whichever address had it before.</summary>
    Task<Result<AddressDto>> SetDefaultAsync(string userId, int id, CancellationToken cancellationToken = default);
}
