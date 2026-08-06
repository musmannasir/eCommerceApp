using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Domain.Configuration;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECommerceApp.Infrastructure.Configuration;

/// <summary>
/// Ensures exactly one <see cref="StoreSettings"/> row exists. Seeds it from
/// appsettings.json's legacy "Store" section (still present at first-run
/// time) so upgrading an existing deployment is behavior-preserving - the
/// admin-editable row starts out identical to what static configuration used
/// to declare. Mirrors <see cref="ECommerceApp.Infrastructure.Security.RoleAndAdminSeeder"/>'s
/// shape and registration.
/// </summary>
public sealed class StoreSettingsSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ILogger<StoreSettingsSeeder> _logger;

    public StoreSettingsSeeder(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        IClock clock,
        ILogger<StoreSettingsSeeder> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _clock = clock;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _dbContext.StoreSettings.AnyAsync(cancellationToken))
        {
            return;
        }

        var settings = new StoreSettings
        {
            StoreName = _configuration["Store:Name"] ?? "ECommerce Store",
            Currency = _configuration["Store:Currency"] ?? "PKR",
            DefaultCountry = _configuration["Store:DefaultCountry"] ?? "Pakistan",
            PricesIncludeTax = _configuration.GetValue("Store:PricesIncludeTax", false),
            RecentlyViewedMaxItems = _configuration.GetValue("Store:RecentlyViewedMaxItems", 10),
            DefaultTaxCountryCode = _configuration["Store:DefaultTaxCountryCode"] ?? "PK",
            DefaultTaxRegionCode = _configuration["Store:DefaultTaxRegionCode"],
            DefaultShippingCountryCode = _configuration["Store:DefaultShippingCountryCode"] ?? "PK",
            DefaultShippingRegionCode = _configuration["Store:DefaultShippingRegionCode"],
            CreatedAtUtc = _clock.UtcNow,
        };

        _dbContext.StoreSettings.Add(settings);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed the initial StoreSettings row.");
        }
    }
}
