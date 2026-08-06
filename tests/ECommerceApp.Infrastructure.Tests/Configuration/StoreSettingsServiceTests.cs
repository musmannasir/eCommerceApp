using ECommerceApp.Application.Configuration.Models;
using ECommerceApp.Domain.Configuration;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Configuration;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerceApp.Infrastructure.Tests.Configuration;

public class StoreSettingsServiceTests : IDisposable
{
    private readonly TestDbContext _dbContext;
    private readonly FakeClock _clock = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly StoreSettingsService _service;

    public StoreSettingsServiceTests()
    {
        var options = new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TestDbContext(options, new FakeCurrentUserService(), _clock);
        _service = new StoreSettingsService(_dbContext, _clock, _cache);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public async Task GetAsync_falls_back_to_defaults_when_no_row_exists()
    {
        var result = await _service.GetAsync();

        result.StoreName.Should().Be("ECommerce Store");
        result.PricesIncludeTax.Should().BeFalse();
        result.RecentlyViewedMaxItems.Should().Be(10);
    }

    [Fact]
    public async Task GetAsync_returns_the_seeded_row_when_one_exists()
    {
        _dbContext.StoreSettings.Add(new StoreSettings
        {
            StoreName = "Acme Store",
            Currency = "USD",
            DefaultCountry = "USA",
            PricesIncludeTax = true,
            RecentlyViewedMaxItems = 5,
            DefaultTaxCountryCode = "US",
            DefaultShippingCountryCode = "US",
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetAsync();

        result.StoreName.Should().Be("Acme Store");
        result.PricesIncludeTax.Should().BeTrue();
        result.RecentlyViewedMaxItems.Should().Be(5);
    }

    [Fact]
    public async Task GetAsync_does_not_hit_the_database_again_after_the_first_call()
    {
        _dbContext.StoreSettings.Add(new StoreSettings
        {
            StoreName = "Acme Store",
            Currency = "USD",
            DefaultCountry = "USA",
            DefaultTaxCountryCode = "US",
            DefaultShippingCountryCode = "US",
        });
        await _dbContext.SaveChangesAsync();

        var first = await _service.GetAsync();

        _dbContext.StoreSettings.RemoveRange(_dbContext.StoreSettings);
        await _dbContext.SaveChangesAsync();

        var second = await _service.GetAsync();

        second.StoreName.Should().Be(first.StoreName).And.Be("Acme Store");
    }

    [Fact]
    public async Task UpdateAsync_persists_changes_and_writes_an_audit_event()
    {
        var request = new UpdateStoreSettingsRequest(
            "New Name", "USD", "USA", true, 20, "US", "CA", "US", "CA", Array.Empty<byte>());

        var result = await _service.UpdateAsync(request, "admin-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.StoreName.Should().Be("New Name");

        var storedRow = await _dbContext.StoreSettings.AsNoTracking().SingleAsync();
        storedRow.StoreName.Should().Be("New Name");

        var auditEvent = await _dbContext.SecurityAuditEvents.SingleOrDefaultAsync(e => e.EventType == SecurityEventType.StoreSettingsUpdated);
        auditEvent.Should().NotBeNull();
        auditEvent!.UserId.Should().Be("admin-1");
    }

    [Fact]
    public async Task UpdateAsync_invalidates_the_cache_so_the_next_GetAsync_reflects_the_change()
    {
        await _service.GetAsync();

        var request = new UpdateStoreSettingsRequest(
            "Updated Name", "USD", "USA", false, 10, "US", "", "US", "", Array.Empty<byte>());
        await _service.UpdateAsync(request, "admin-1");

        var result = await _service.GetAsync();

        result.StoreName.Should().Be("Updated Name");
    }

}
