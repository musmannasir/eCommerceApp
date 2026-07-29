using ECommerceApp.Application.Addresses;
using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Domain.Addresses;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Addresses;

/// <summary>
/// Queries ApplicationDbContext directly, the same convention every other
/// Storefront service follows. Every method scopes its query by UserId - an
/// id belonging to a different user is indistinguishable from one that
/// doesn't exist at all, so both return NotFound rather than Forbidden.
/// </summary>
public sealed class AddressService : IAddressService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;

    public AddressService(ApplicationDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var addresses = await _dbContext.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return addresses.Select(ToDto).ToList();
    }

    public async Task<Result<AddressDto>> GetByIdAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var address = await FindOwnedAsync(userId, id, cancellationToken);
        return address is null
            ? Result.Failure<AddressDto>(NotFound())
            : Result.Success(ToDto(address));
    }

    public async Task<Result<AddressDto>> CreateAsync(string userId, CreateAddressRequest request, CancellationToken cancellationToken = default)
    {
        var hasAnyAddress = await _dbContext.Addresses.AnyAsync(a => a.UserId == userId, cancellationToken);
        // A customer's very first address is always the default - there's
        // nothing to compare it against, and leaving them with zero default
        // addresses right after saving their first one would be surprising.
        var isDefault = !hasAnyAddress || request.IsDefault;

        if (isDefault && hasAnyAddress)
        {
            await ClearExistingDefaultAsync(userId, cancellationToken);
        }

        var now = _clock.UtcNow;
        var address = new Address
        {
            UserId = userId,
            Label = request.Label,
            FullName = request.FullName,
            Phone = request.Phone,
            Line1 = request.Line1,
            Line2 = request.Line2,
            City = request.City,
            RegionCode = request.RegionCode,
            PostalCode = request.PostalCode,
            CountryCode = request.CountryCode,
            IsDefault = isDefault,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _dbContext.Addresses.Add(address);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(address));
    }

    public async Task<Result<AddressDto>> UpdateAsync(string userId, UpdateAddressRequest request, CancellationToken cancellationToken = default)
    {
        var address = await FindOwnedAsync(userId, request.Id, cancellationToken);
        if (address is null)
        {
            return Result.Failure<AddressDto>(NotFound());
        }

        if (request.IsDefault && !address.IsDefault)
        {
            await ClearExistingDefaultAsync(userId, cancellationToken);
        }

        address.Label = request.Label;
        address.FullName = request.FullName;
        address.Phone = request.Phone;
        address.Line1 = request.Line1;
        address.Line2 = request.Line2;
        address.City = request.City;
        address.RegionCode = request.RegionCode;
        address.PostalCode = request.PostalCode;
        address.CountryCode = request.CountryCode;
        address.IsDefault = request.IsDefault;
        address.UpdatedAtUtc = _clock.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(address));
    }

    public async Task<Result> DeleteAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var address = await FindOwnedAsync(userId, id, cancellationToken);
        if (address is null)
        {
            return Result.Failure(NotFound());
        }

        // No default is silently promoted - the customer picks a new one
        // explicitly via SetDefaultAsync if they deleted their default.
        _dbContext.Addresses.Remove(address);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<AddressDto>> SetDefaultAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var address = await FindOwnedAsync(userId, id, cancellationToken);
        if (address is null)
        {
            return Result.Failure<AddressDto>(NotFound());
        }

        if (!address.IsDefault)
        {
            await ClearExistingDefaultAsync(userId, cancellationToken);
            address.IsDefault = true;
            address.UpdatedAtUtc = _clock.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(ToDto(address));
    }

    private async Task ClearExistingDefaultAsync(string userId, CancellationToken cancellationToken)
    {
        var currentDefault = await _dbContext.Addresses
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault, cancellationToken);

        if (currentDefault is not null)
        {
            currentDefault.IsDefault = false;
            currentDefault.UpdatedAtUtc = _clock.UtcNow;
        }
    }

    private Task<Address?> FindOwnedAsync(string userId, int id, CancellationToken cancellationToken) =>
        _dbContext.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

    private static Error NotFound() => Error.NotFound("address.not_found", "This address could not be found.");

    private static AddressDto ToDto(Address address) => new(
        address.Id,
        address.Label,
        address.FullName,
        address.Phone,
        address.Line1,
        address.Line2,
        address.City,
        address.RegionCode,
        address.PostalCode,
        address.CountryCode,
        address.IsDefault);
}
