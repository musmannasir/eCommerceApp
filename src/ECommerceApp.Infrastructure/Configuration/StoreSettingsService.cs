using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Configuration;
using ECommerceApp.Application.Configuration.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Configuration;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerceApp.Infrastructure.Configuration;

/// <summary>
/// Milestone 16.3. Cached via <see cref="IMemoryCache"/> - unlike Tax/Shipping's
/// occasional reads, <see cref="GetAsync"/> is called by every storefront
/// and admin page's layout, so an uncached DB round trip per request would
/// be real, avoidable overhead for one small, low-churn row - the same
/// reasoning category-nav caching (Milestone 4.3) already established. The
/// cache is invalidated explicitly on <see cref="UpdateAsync"/> rather than
/// left to expire, so an admin's own change is reflected immediately.
/// </summary>
public sealed class StoreSettingsService : IStoreSettingsService
{
    private const string CacheKey = "StoreSettings:Current";

    /// <summary>
    /// Matches exactly what appsettings.json's "Store" section used to
    /// declare - if the seeded row is ever missing (a fresh database that
    /// hasn't run <see cref="StoreSettingsSeeder"/> yet, or a seeding
    /// failure at startup), every page layout still has to render, so this
    /// falls back to the same values rather than throwing.
    /// </summary>
    private static readonly StoreSettingsDto DefaultSettings = new(
        "ECommerce Store", "PKR", "Pakistan", false, 10, "PK", "", "PK", "", Array.Empty<byte>());

    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IMemoryCache _cache;

    public StoreSettingsService(ApplicationDbContext dbContext, IClock clock, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _clock = clock;
        _cache = cache;
    }

    public async Task<StoreSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out StoreSettingsDto? cached) && cached is not null)
        {
            return cached;
        }

        var settings = await _dbContext.StoreSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var dto = settings is null ? DefaultSettings : ToDto(settings);

        _cache.Set(CacheKey, dto, TimeSpan.FromMinutes(10));
        return dto;
    }

    public async Task<Result<StoreSettingsDto>> UpdateAsync(UpdateStoreSettingsRequest request, string actingAdminUserId, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.StoreSettings.FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new StoreSettings { CreatedAtUtc = _clock.UtcNow };
            _dbContext.StoreSettings.Add(settings);
        }
        else
        {
            // Compares what the admin's form last saw against the row's
            // actual current RowVersion at save time - if someone else saved
            // a change in between, SaveChangesAsync throws below rather than
            // silently overwriting their edit.
            _dbContext.Entry(settings).Property(s => s.RowVersion).OriginalValue = request.RowVersion;
        }

        settings.StoreName = request.StoreName;
        settings.Currency = request.Currency;
        settings.DefaultCountry = request.DefaultCountry;
        settings.PricesIncludeTax = request.PricesIncludeTax;
        settings.RecentlyViewedMaxItems = request.RecentlyViewedMaxItems;
        settings.DefaultTaxCountryCode = request.DefaultTaxCountryCode;
        settings.DefaultTaxRegionCode = request.DefaultTaxRegionCode;
        settings.DefaultShippingCountryCode = request.DefaultShippingCountryCode;
        settings.DefaultShippingRegionCode = request.DefaultShippingRegionCode;

        _dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            UserId = actingAdminUserId,
            EventType = SecurityEventType.StoreSettingsUpdated,
            Succeeded = true,
            OccurredAtUtc = _clock.UtcNow,
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<StoreSettingsDto>(Error.Conflict(
                "storeSettings.concurrency_conflict", "Settings were changed by someone else - please reload and try again."));
        }

        _cache.Remove(CacheKey);

        return Result.Success(ToDto(settings));
    }

    private static StoreSettingsDto ToDto(StoreSettings s) => new(
        s.StoreName, s.Currency, s.DefaultCountry, s.PricesIncludeTax, s.RecentlyViewedMaxItems,
        s.DefaultTaxCountryCode, s.DefaultTaxRegionCode, s.DefaultShippingCountryCode, s.DefaultShippingRegionCode,
        s.RowVersion);
}
