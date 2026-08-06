using System.Security.Claims;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Configuration;
using ECommerceApp.Application.Storefront;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Storefront;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Web.Services;

/// <summary>
/// Lives in the Web project, not Infrastructure, because it needs HttpContext
/// (for the guest cookie) - the same reasoning CurrentUserService already
/// follows. Guests: a single cookie holding comma-separated product IDs,
/// most-recent-first, nothing else - no name, no session token, nothing
/// personally identifying. Authenticated: a DB row per (user, product),
/// upserted and trimmed to the configured max on every view.
/// </summary>
public sealed class RecentlyViewedService : IRecentlyViewedService
{
    private const string CookieName = "RecentlyViewed";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IStoreSettingsService _storeSettingsService;
    private readonly IWebHostEnvironment _environment;

    public RecentlyViewedService(
        IHttpContextAccessor httpContextAccessor,
        ApplicationDbContext dbContext,
        IClock clock,
        IStoreSettingsService storeSettingsService,
        IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _clock = clock;
        _storeSettingsService = storeSettingsService;
        _environment = environment;
    }

    public async Task RecordViewAsync(int productId, CancellationToken cancellationToken = default)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is not null)
        {
            await RecordForAuthenticatedUserAsync(userId, productId, cancellationToken);
        }
        else
        {
            var maxItems = (await _storeSettingsService.GetAsync(cancellationToken)).RecentlyViewedMaxItems;
            RecordForGuest(productId, maxItems);
        }
    }

    public async Task<IReadOnlyList<HomeProductCardDto>> GetRecentlyViewedAsync(int? excludeProductId = null, CancellationToken cancellationToken = default)
    {
        var userId = GetAuthenticatedUserId();
        var maxItems = (await _storeSettingsService.GetAsync(cancellationToken)).RecentlyViewedMaxItems;
        var orderedProductIds = userId is not null
            ? await _dbContext.RecentlyViewedItems
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.ViewedAtUtc)
                .Select(r => r.ProductId)
                .Take(maxItems)
                .ToListAsync(cancellationToken)
            : ReadGuestCookie();

        if (excludeProductId.HasValue)
        {
            orderedProductIds = orderedProductIds.Where(id => id != excludeProductId.Value).ToList();
        }

        if (orderedProductIds.Count == 0)
        {
            return Array.Empty<HomeProductCardDto>();
        }

        // A product might have been unpublished/deactivated/deleted since it was viewed -
        // this query naturally excludes those (IsActive/IsPublished filter, soft-delete
        // global query filter), so the history gracefully skips them rather than erroring.
        var cards = await _dbContext.Products
            .Where(p => orderedProductIds.Contains(p.Id) && p.IsActive && p.IsPublished)
            .AsNoTracking()
            .Select(p => new HomeProductCardDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Images.Where(i => i.IsPrimary).Select(i => i.Path).FirstOrDefault() ?? p.Images.Select(i => i.Path).FirstOrDefault(),
                p.Brand != null ? p.Brand.Name : null,
                p.Brand != null ? p.Brand.Slug : null,
                p.SellingPrice,
                p.CompareAtPrice,
                _dbContext.InventoryItems.Any(i => i.ProductId == p.Id) &&
                    !_dbContext.InventoryItems.Any(i => i.ProductId == p.Id && (i.QuantityOnHand - i.QuantityReserved > 0 || i.AllowBackorder))))
            .ToListAsync(cancellationToken);

        var cardsById = cards.ToDictionary(c => c.Id);
        return orderedProductIds.Where(cardsById.ContainsKey).Select(id => cardsById[id]).ToList();
    }

    private string? GetAuthenticatedUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    }

    private async Task RecordForAuthenticatedUserAsync(string userId, int productId, CancellationToken cancellationToken)
    {
        var utcNow = _clock.UtcNow;
        var existing = await _dbContext.RecentlyViewedItems
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == productId, cancellationToken);

        if (existing is not null)
        {
            existing.ViewedAtUtc = utcNow;
        }
        else
        {
            _dbContext.RecentlyViewedItems.Add(new RecentlyViewedItem { UserId = userId, ProductId = productId, ViewedAtUtc = utcNow });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var maxItems = (await _storeSettingsService.GetAsync(cancellationToken)).RecentlyViewedMaxItems;
        var excess = await _dbContext.RecentlyViewedItems
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ViewedAtUtc)
            .Skip(maxItems)
            .ToListAsync(cancellationToken);

        if (excess.Count > 0)
        {
            _dbContext.RecentlyViewedItems.RemoveRange(excess);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private void RecordForGuest(int productId, int maxItems)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var ids = ReadGuestCookie();
        ids.Remove(productId);
        ids.Insert(0, productId);
        if (ids.Count > maxItems)
        {
            ids = ids.Take(maxItems).ToList();
        }

        httpContext.Response.Cookies.Append(CookieName, string.Join(',', ids), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = !_environment.IsDevelopment(),
            Expires = DateTimeOffset.UtcNow.AddDays(90),
            IsEssential = false,
        });
    }

    private List<int> ReadGuestCookie()
    {
        var raw = _httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(raw))
        {
            return new List<int>();
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }
}
