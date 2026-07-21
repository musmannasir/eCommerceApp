using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Application.Wishlist;
using ECommerceApp.Application.Wishlist.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Wishlist;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Wishlist;

/// <summary>
/// Queries ApplicationDbContext directly, the same convention every other
/// Storefront service follows. A product that's since been unpublished,
/// deactivated, or soft-deleted is silently excluded from the list - same
/// reasoning as RecentlyViewedService, not Cart's "keep it visible but
/// flagged" approach, since a wishlist is browsing-adjacent rather than a
/// committed purchase intent. The HomeProductCardDto projection is built
/// inline (not via a helper method call) so EF Core can translate it to
/// SQL - the same "InMemory would let a method call through, the real
/// provider won't" caution every other Storefront service already follows.
/// </summary>
public sealed class WishlistService : IWishlistService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;

    public WishlistService(ApplicationDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<WishlistDto> GetWishlistAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.WishlistItems
            .Where(w => w.UserId == userId && w.Product.IsActive && w.Product.IsPublished)
            .OrderByDescending(w => w.AddedAtUtc)
            .Select(w => new
            {
                w.Id,
                w.AddedAtUtc,
                Card = new HomeProductCardDto(
                    w.Product.Id,
                    w.Product.Name,
                    w.Product.Slug,
                    w.Product.Images.Where(i => i.IsPrimary).Select(i => i.Path).FirstOrDefault() ?? w.Product.Images.Select(i => i.Path).FirstOrDefault(),
                    w.Product.Brand != null ? w.Product.Brand.Name : null,
                    w.Product.Brand != null ? w.Product.Brand.Slug : null,
                    w.Product.SellingPrice,
                    w.Product.CompareAtPrice,
                    _dbContext.InventoryItems.Any(i => i.ProductId == w.Product.Id) &&
                        !_dbContext.InventoryItems.Any(i => i.ProductId == w.Product.Id && (i.QuantityOnHand - i.QuantityReserved > 0 || i.AllowBackorder))),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new WishlistItemDto(r.Id, r.Card, r.AddedAtUtc)).ToList();
        return new WishlistDto(items);
    }

    public async Task<Result<WishlistToggleResultDto>> ToggleAsync(string userId, int productId, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive && p.IsPublished, cancellationToken);

        if (product is null)
        {
            return Result.Failure<WishlistToggleResultDto>(Error.NotFound("wishlist.product_not_found", "This product is not available."));
        }

        var existing = await _dbContext.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId, cancellationToken);

        bool isWishlisted;
        if (existing is not null)
        {
            _dbContext.WishlistItems.Remove(existing);
            isWishlisted = false;
        }
        else
        {
            _dbContext.WishlistItems.Add(new WishlistItem { UserId = userId, ProductId = productId, AddedAtUtc = _clock.UtcNow });
            isWishlisted = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var itemCount = await _dbContext.WishlistItems.CountAsync(w => w.UserId == userId, cancellationToken);

        return Result.Success(new WishlistToggleResultDto(isWishlisted, itemCount));
    }

    public async Task<WishlistToggleResultDto> RemoveItemAsync(string userId, int productId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId, cancellationToken);

        if (existing is not null)
        {
            _dbContext.WishlistItems.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var itemCount = await _dbContext.WishlistItems.CountAsync(w => w.UserId == userId, cancellationToken);
        return new WishlistToggleResultDto(false, itemCount);
    }

    public Task<bool> IsWishlistedAsync(string userId, int productId, CancellationToken cancellationToken = default) =>
        _dbContext.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId, cancellationToken);
}
