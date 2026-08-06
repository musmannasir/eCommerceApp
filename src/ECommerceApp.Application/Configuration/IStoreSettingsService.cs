using ECommerceApp.Application.Configuration.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Configuration;

/// <summary>
/// Milestone 16.3 - the store-wide settings that used to live only in
/// appsettings.json's static "Store" section, now admin-editable at
/// runtime. <see cref="GetAsync"/> never fails - callers include every
/// storefront/admin page layout, so a missing settings row falls back to
/// the same defaults appsettings.json used to declare rather than breaking
/// every page render.
/// </summary>
public interface IStoreSettingsService
{
    Task<StoreSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<Result<StoreSettingsDto>> UpdateAsync(UpdateStoreSettingsRequest request, string actingAdminUserId, CancellationToken cancellationToken = default);
}
