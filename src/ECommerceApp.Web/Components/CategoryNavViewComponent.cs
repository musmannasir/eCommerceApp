using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Catalog.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerceApp.Web.Components;

/// <summary>
/// Renders the public header's category navigation from real catalog data.
/// Links to /Category/{slug} as of Milestone 4.2. The active category tree
/// is cached in-process (Milestone 4.3's "caching for stable navigation
/// data") - it's rendered on every single page, but categories change
/// rarely via a handful of admin actions, so a short TTL without explicit
/// write-path invalidation is a reasonable tradeoff (Admin's own category
/// tree view bypasses this cache entirely, so admins always see live data
/// while editing).
/// </summary>
public class CategoryNavViewComponent : ViewComponent
{
    private const string CacheKey = "storefront:category-nav";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly ICategoryService _categoryService;
    private readonly IMemoryCache _cache;

    public CategoryNavViewComponent(ICategoryService categoryService, IMemoryCache cache)
    {
        _categoryService = categoryService;
        _cache = cache;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var activeTopLevel = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var result = await _categoryService.GetTreeAsync();
            return result.IsSuccess ? FilterActive(result.Value) : Array.Empty<CategoryTreeNodeDto>();
        });

        return View(activeTopLevel);
    }

    /// <summary>GetTreeAsync returns the full tree (active and inactive) for Admin use; the
    /// public nav must only show active categories, and an inactive parent hides its
    /// children too, even if a child is individually marked active.</summary>
    private static IReadOnlyList<CategoryTreeNodeDto> FilterActive(IReadOnlyList<CategoryTreeNodeDto> nodes) =>
        nodes
            .Where(n => n.IsActive)
            .Select(n => n with { Children = FilterActive(n.Children) })
            .ToList();
}
