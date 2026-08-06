using ECommerceApp.Application.Configuration;
using ECommerceApp.Application.Configuration.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Infrastructure.Tests.TestSupport;

/// <summary>
/// In-memory double for tests that only need GetAsync's current values (Tax/Shipping
/// default jurisdiction, tax-inclusive pricing) - mirrors appsettings.json's old
/// "Store" defaults so existing test expectations keep working unchanged.
/// </summary>
public class FakeStoreSettingsService : IStoreSettingsService
{
    public StoreSettingsDto Settings { get; set; } = new(
        "ECommerce Store", "PKR", "Pakistan", false, 10, "US", "CA", "US", "CA", Array.Empty<byte>());

    public Task<StoreSettingsDto> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);

    public Task<Result<StoreSettingsDto>> UpdateAsync(UpdateStoreSettingsRequest request, string actingAdminUserId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by current tests.");
}
