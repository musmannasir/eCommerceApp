using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Storefront;

public interface IProductDetailService
{
    /// <summary>
    /// selectedVariantId takes precedence over selectedAttributeValueIds if both are
    /// given. If neither resolves to a variant (or the product has no variants at
    /// all), the base product's own SKU/price/stock are shown. userId is null for an
    /// anonymous visitor - IsWishlisted and HasReviewed are always false in that case
    /// (Milestone 6.3's wishlist and Milestone 12.1's reviews are both account-only).
    /// reviewsPage pages the Reviews tab's list independently of the rest of the page.
    /// </summary>
    Task<Result<ProductDetailDto>> GetDetailAsync(
        string slug,
        int? selectedVariantId,
        IReadOnlyList<int> selectedAttributeValueIds,
        string? userId = null,
        int reviewsPage = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Strict, server-authoritative lookup backing the live (AJAX) variant switcher -
    /// fails if variantId doesn't exist, isn't active, or doesn't belong to the product
    /// identified by slug, rather than falling back to anything.
    /// </summary>
    Task<Result<VariantResolutionDto>> ResolveVariantAsync(string slug, int variantId, CancellationToken cancellationToken = default);
}
