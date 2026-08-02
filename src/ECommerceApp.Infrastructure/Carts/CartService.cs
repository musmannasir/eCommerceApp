using ECommerceApp.Application.Carts;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Marketing;
using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Application.Pricing;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Carts;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Carts;

/// <summary>
/// Cart core (Milestone 6.1). Queries ApplicationDbContext directly, the same
/// convention every other Storefront service follows. Has no HttpContext
/// dependency - the caller (CartController, in Web) resolves who owns the cart
/// and passes a CartOwner in, so this stays Infrastructure-hosted like
/// ProductDetailService/RecommendationService rather than needing the
/// Web-hosted exception RecentlyViewedService required.
/// </summary>
public sealed class CartService : ICartService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPricingService _pricingService;
    private readonly IPromotionService _promotionService;
    private readonly ICheckoutCalculationService _checkoutCalculationService;
    private readonly IClock _clock;

    public CartService(
        ApplicationDbContext dbContext, IPricingService pricingService, IPromotionService promotionService,
        ICheckoutCalculationService checkoutCalculationService, IClock clock)
    {
        _dbContext = dbContext;
        _pricingService = pricingService;
        _promotionService = promotionService;
        _checkoutCalculationService = checkoutCalculationService;
        _clock = clock;
    }

    public async Task<CartDto> GetCartAsync(CartOwner owner, CancellationToken cancellationToken = default)
    {
        var cart = await FindCartAsync(owner, cancellationToken);
        return cart is null
            ? EmptyCart()
            : await BuildCartDtoAsync(cart.Id, cancellationToken);
    }

    public async Task<Result<CartDto>> AddItemAsync(CartOwner owner, AddCartItemRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Where(p => p.Id == request.ProductId && p.IsActive && p.IsPublished)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return Result.Failure<CartDto>(Error.NotFound("cart.product_not_found", "This product is not available."));
        }

        var activeVariants = product.Variants.Where(v => v.IsActive).ToList();
        ProductVariant? variant = null;

        if (activeVariants.Count > 0)
        {
            if (!request.ProductVariantId.HasValue)
            {
                return Result.Failure<CartDto>(Error.Validation(
                    "cart.variant_required", "Please select an option before adding this product to your cart."));
            }

            variant = activeVariants.FirstOrDefault(v => v.Id == request.ProductVariantId.Value);
            if (variant is null)
            {
                return Result.Failure<CartDto>(Error.Validation(
                    "cart.variant_invalid", "The selected option is not available for this product."));
            }
        }
        else if (request.ProductVariantId.HasValue)
        {
            return Result.Failure<CartDto>(Error.Validation(
                "cart.variant_not_applicable", "This product does not have selectable options."));
        }

        var cart = await GetOrCreateCartAsync(owner, cancellationToken);
        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId && i.ProductVariantId == variant?.Id);
        var requestedTotalQuantity = (existingItem?.Quantity ?? 0) + request.Quantity;

        var (_, available, allowBackorder) = await GetStockAsync(product.Id, variant?.Id, product.LowStockThreshold, cancellationToken);
        if (!allowBackorder && requestedTotalQuantity > available)
        {
            return Result.Failure<CartDto>(Error.Validation("cart.insufficient_stock", StockMessage(available)));
        }

        // Re-adding (or first adding) always re-stamps PriceWhenAdded to the
        // current live price - the customer is looking at today's price right
        // now, so there's nothing stale to flag until it changes again later.
        var currentPrice = _pricingService.Calculate(product.SellingPrice, product.CompareAtPrice, variant?.SellingPrice, variant?.CompareAtPrice).FinalPrice;

        if (existingItem is not null)
        {
            existingItem.Quantity = requestedTotalQuantity;
            existingItem.PriceWhenAdded = currentPrice;
        }
        else
        {
            _dbContext.CartItems.Add(new CartItem
            {
                CartId = cart.Id,
                ProductId = product.Id,
                ProductVariantId = variant?.Id,
                Quantity = request.Quantity,
                PriceWhenAdded = currentPrice,
                AddedAtUtc = _clock.UtcNow,
            });
        }

        cart.UpdatedAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await BuildCartDtoAsync(cart.Id, cancellationToken));
    }

    public async Task<Result<CartDto>> UpdateQuantityAsync(CartOwner owner, UpdateCartItemQuantityRequest request, CancellationToken cancellationToken = default)
    {
        var cart = await FindCartAsync(owner, cancellationToken);
        var item = cart?.Items.FirstOrDefault(i => i.Id == request.CartItemId);
        if (cart is null || item is null)
        {
            return Result.Failure<CartDto>(Error.NotFound("cart.item_not_found", "This item is no longer in your cart."));
        }

        var product = await _dbContext.Products
            .Where(p => p.Id == item.ProductId)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(cancellationToken);

        var variant = item.ProductVariantId.HasValue
            ? product?.Variants.FirstOrDefault(v => v.Id == item.ProductVariantId.Value)
            : null;

        var isAvailable = product is { IsActive: true, IsPublished: true } &&
            (item.ProductVariantId is null || variant is { IsActive: true });

        if (!isAvailable)
        {
            return Result.Failure<CartDto>(Error.Validation(
                "cart.item_unavailable", "This item is no longer available - remove it from your cart."));
        }

        var (_, available, allowBackorder) = await GetStockAsync(item.ProductId, item.ProductVariantId, product!.LowStockThreshold, cancellationToken);
        if (!allowBackorder && request.Quantity > available)
        {
            return Result.Failure<CartDto>(Error.Validation("cart.insufficient_stock", StockMessage(available)));
        }

        item.Quantity = request.Quantity;
        // An explicit quantity change is a fresh look at the line, same as a
        // re-add - re-stamp the price so a stale PriceChanged notice doesn't
        // linger after the customer has already acted on this line.
        item.PriceWhenAdded = _pricingService.Calculate(product!.SellingPrice, product.CompareAtPrice, variant?.SellingPrice, variant?.CompareAtPrice).FinalPrice;
        cart.UpdatedAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await BuildCartDtoAsync(cart.Id, cancellationToken));
    }

    public async Task<Result<CartDto>> RemoveItemAsync(CartOwner owner, int cartItemId, CancellationToken cancellationToken = default)
    {
        var cart = await FindCartAsync(owner, cancellationToken);
        var item = cart?.Items.FirstOrDefault(i => i.Id == cartItemId);
        if (cart is null || item is null)
        {
            return Result.Failure<CartDto>(Error.NotFound("cart.item_not_found", "This item is no longer in your cart."));
        }

        _dbContext.CartItems.Remove(item);
        cart.UpdatedAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await BuildCartDtoAsync(cart.Id, cancellationToken));
    }

    public async Task<CartDto> ClearCartAsync(CartOwner owner, CancellationToken cancellationToken = default)
    {
        var cart = await FindCartAsync(owner, cancellationToken);
        if (cart is null)
        {
            return EmptyCart();
        }

        _dbContext.CartItems.RemoveRange(cart.Items);
        cart.UpdatedAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildCartDtoAsync(cart.Id, cancellationToken);
    }

    public async Task<CartDto> MergeGuestCartIntoUserCartAsync(string guestToken, string userId, CancellationToken cancellationToken = default)
    {
        var guestCart = await _dbContext.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.GuestToken == guestToken, cancellationToken);
        if (guestCart is null)
        {
            return await GetCartAsync(CartOwner.ForUser(userId), cancellationToken);
        }

        var userCart = await _dbContext.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (userCart is null)
        {
            // Fast path: the user had no cart of their own, so the guest cart
            // just becomes theirs - no line-by-line merge needed.
            guestCart.UserId = userId;
            guestCart.GuestToken = null;
            guestCart.UpdatedAtUtc = _clock.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return await BuildCartDtoAsync(guestCart.Id, cancellationToken);
        }

        foreach (var guestItem in guestCart.Items.ToList())
        {
            var matchingUserItem = userCart.Items.FirstOrDefault(
                i => i.ProductId == guestItem.ProductId && i.ProductVariantId == guestItem.ProductVariantId);

            if (matchingUserItem is null)
            {
                guestItem.CartId = userCart.Id;
                continue;
            }

            // A login can't reasonably fail because of a cart quantity conflict -
            // cap the combined quantity to whatever stock actually allows instead
            // of rejecting the merge outright; the customer can see and adjust it
            // afterward via the same QuantityExceedsStock signal a plain cart read uses.
            var (_, available, allowBackorder) = await GetStockAsync(guestItem.ProductId, guestItem.ProductVariantId, null, cancellationToken);
            var combinedQuantity = matchingUserItem.Quantity + guestItem.Quantity;
            matchingUserItem.Quantity = allowBackorder ? combinedQuantity : Math.Min(combinedQuantity, available);
            // Merging is itself a fresh look at current pricing - same reasoning
            // as re-adding an existing line - so there's no "which one wins"
            // question between the guest and user line's original prices.
            matchingUserItem.PriceWhenAdded = await CurrentPriceAsync(guestItem.ProductId, guestItem.ProductVariantId, cancellationToken);
            _dbContext.CartItems.Remove(guestItem);
        }

        _dbContext.Carts.Remove(guestCart);
        userCart.UpdatedAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildCartDtoAsync(userCart.Id, cancellationToken);
    }

    public async Task<Result<CartDto>> ApplyCouponAsync(CartOwner owner, string couponCode, CancellationToken cancellationToken = default)
    {
        var cart = await FindCartAsync(owner, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return Result.Failure<CartDto>(Error.Validation("cart.empty", "Add items to your cart before applying a coupon."));
        }

        var (_, subtotal, promotionLines, _) = await BuildAvailableLinesAsync(cart.Id, cancellationToken);
        var applicationResult = await _promotionService.FindApplicablePromotionAsync(couponCode, promotionLines, subtotal, cancellationToken);
        if (applicationResult.IsFailure)
        {
            return Result.Failure<CartDto>(applicationResult.FirstError);
        }

        cart.AppliedPromotionId = applicationResult.Value.PromotionId;
        cart.UpdatedAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await BuildCartDtoAsync(cart.Id, cancellationToken));
    }

    public async Task<CartDto> RemoveCouponAsync(CartOwner owner, CancellationToken cancellationToken = default)
    {
        var cart = await FindCartAsync(owner, cancellationToken);
        if (cart is null)
        {
            return EmptyCart();
        }

        cart.AppliedPromotionId = null;
        cart.UpdatedAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildCartDtoAsync(cart.Id, cancellationToken);
    }

    /// <summary>
    /// Builds every per-line shape a caller might need from the cart's
    /// currently-available, priced lines - PromotionCartLine (IPromotionService),
    /// CheckoutLineDto (ICheckoutCalculationService), and the raw subtotal -
    /// shared by ApplyCouponAsync, BuildCartDtoAsync, and GetCheckoutInputAsync
    /// so none of them re-derive this per-line data independently.
    /// </summary>
    private async Task<(List<CartItemDto> AvailableItems, decimal Subtotal, List<PromotionCartLine> PromotionLines, List<CheckoutLineDto> CheckoutLines)> BuildAvailableLinesAsync(
        int cartId, CancellationToken cancellationToken)
    {
        var (itemDtos, products) = await ComputeItemDtosAsync(cartId, cancellationToken);
        return ComputeAvailableLines(itemDtos, products);
    }

    private static (List<CartItemDto> AvailableItems, decimal Subtotal, List<PromotionCartLine> PromotionLines, List<CheckoutLineDto> CheckoutLines) ComputeAvailableLines(
        List<CartItemDto> itemDtos, Dictionary<int, Product> products)
    {
        var availableItems = itemDtos.Where(i => i.IsAvailable).ToList();
        var subtotal = availableItems.Sum(i => i.LineTotal);

        var promotionLines = availableItems
            .Select(i => new PromotionCartLine(i.ProductId, products[i.ProductId].CategoryId, products[i.ProductId].BrandId, i.LineTotal))
            .ToList();

        // A product with no recorded weight contributes 0kg - same leniency
        // untracked inventory already gets, rather than blocking the estimate.
        var checkoutLines = availableItems
            .Select(i => new CheckoutLineDto(
                i.ProductId, products[i.ProductId].CategoryId, products[i.ProductId].BrandId,
                products[i.ProductId].TaxCategory, products[i.ProductId].IsTaxable,
                (products[i.ProductId].Weight ?? 0m) * i.Quantity, i.LineTotal))
            .ToList();

        return (availableItems, subtotal, promotionLines, checkoutLines);
    }

    /// <summary>
    /// The cart's currently-available lines plus the resolved applied
    /// promotion id, for the Checkout flow (Milestone 8.2) to compute real
    /// destination-based totals via ICheckoutCalculationService.CalculateAsync
    /// against a selected Address, instead of the store-default-jurisdiction
    /// estimate the Cart page shows.
    /// </summary>
    public async Task<Result<CheckoutInputDto>> GetCheckoutInputAsync(CartOwner owner, CancellationToken cancellationToken = default)
    {
        var cart = await FindCartAsync(owner, cancellationToken);
        if (cart is null)
        {
            return Result.Failure<CheckoutInputDto>(Error.Validation("cart.empty", "Add items to your cart before checking out."));
        }

        var (_, subtotal, promotionLines, checkoutLines) = await BuildAvailableLinesAsync(cart.Id, cancellationToken);
        if (checkoutLines.Count == 0)
        {
            return Result.Failure<CheckoutInputDto>(Error.Validation("cart.empty", "Add items to your cart before checking out."));
        }

        var (_, _, _, promotionId) = await ResolveAppliedPromotionAsync(cart.Id, promotionLines, subtotal, cancellationToken);

        return Result.Success(new CheckoutInputDto(checkoutLines, promotionId));
    }

    public async Task<ReorderResultDto> ReorderAsync(CartOwner owner, IReadOnlyList<ReorderItemRequest> items, CancellationToken cancellationToken = default)
    {
        var skipped = new List<ReorderSkippedItemDto>();
        var addedCount = 0;

        foreach (var item in items)
        {
            var result = await AddItemAsync(owner, new AddCartItemRequest(item.ProductId, item.ProductVariantId, item.Quantity), cancellationToken);
            if (result.IsFailure)
            {
                skipped.Add(new ReorderSkippedItemDto(item.ProductName, result.FirstError.Message));
            }
            else
            {
                addedCount++;
            }
        }

        var cart = await GetCartAsync(owner, cancellationToken);
        return new ReorderResultDto(cart, addedCount, skipped);
    }

    /// <summary>
    /// Re-validates the cart's applied promotion (if any) against its current
    /// lines/subtotal via IPromotionService, silently clearing it if it's no
    /// longer valid - see Cart.AppliedPromotionId's doc comment. Also returns
    /// the resolved (valid-or-null) promotion id, so the caller can pass it
    /// straight to ICheckoutCalculationService without re-deriving it.
    /// </summary>
    private async Task<(string? CouponCode, string? PromotionName, decimal Discount, int? PromotionId)> ResolveAppliedPromotionAsync(
        int cartId, IReadOnlyList<PromotionCartLine> lines, decimal subtotal, CancellationToken cancellationToken)
    {
        var cart = await _dbContext.Carts.FirstOrDefaultAsync(c => c.Id == cartId, cancellationToken);
        if (cart?.AppliedPromotionId is not { } promotionId)
        {
            return (null, null, 0, null);
        }

        var validation = await _promotionService.ValidateAppliedPromotionAsync(promotionId, lines, subtotal, cancellationToken);
        if (validation.IsFailure)
        {
            cart.AppliedPromotionId = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return (null, null, 0, null);
        }

        return (validation.Value.CouponCode, validation.Value.Name, validation.Value.DiscountAmount, promotionId);
    }

    private static CartDto EmptyCart() => new(null, Array.Empty<CartItemDto>(), 0, 0, null, null, 0, 0, 0, false, 0, false, 0);

    private async Task<decimal> CurrentPriceAsync(int productId, int? variantId, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .Where(p => p.Id == productId)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(cancellationToken);
        var variant = variantId.HasValue ? product?.Variants.FirstOrDefault(v => v.Id == variantId.Value) : null;

        return _pricingService.Calculate(
            product?.SellingPrice ?? 0m, product?.CompareAtPrice, variant?.SellingPrice, variant?.CompareAtPrice).FinalPrice;
    }

    private static string StockMessage(int available) =>
        available <= 0 ? "This item is currently out of stock." : $"Only {available} left in stock.";

    private async Task<Cart?> FindCartAsync(CartOwner owner, CancellationToken cancellationToken) =>
        owner.UserId is not null
            ? await _dbContext.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == owner.UserId, cancellationToken)
            : await _dbContext.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.GuestToken == owner.GuestToken, cancellationToken);

    private async Task<Cart> GetOrCreateCartAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        var cart = await FindCartAsync(owner, cancellationToken);
        if (cart is not null)
        {
            return cart;
        }

        cart = new Cart
        {
            UserId = owner.UserId,
            GuestToken = owner.GuestToken,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
        };
        _dbContext.Carts.Add(cart);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return cart;
    }

    private async Task<CartDto> BuildCartDtoAsync(int cartId, CancellationToken cancellationToken)
    {
        var (itemDtos, products) = await ComputeItemDtosAsync(cartId, cancellationToken);

        if (itemDtos.Count == 0)
        {
            // No lines left to evaluate a promotion against - clear it the
            // same way an invalid one gets cleared below, rather than leaving
            // a discount attached to a cart with nothing in it.
            await ResolveAppliedPromotionAsync(cartId, Array.Empty<PromotionCartLine>(), 0, cancellationToken);
            return new CartDto(cartId, Array.Empty<CartItemDto>(), 0, 0, null, null, 0, 0, 0, false, 0, false, 0);
        }

        var (availableItems, subtotal, lines, checkoutLines) = ComputeAvailableLines(itemDtos, products);

        var (couponCode, promotionName, discount, resolvedPromotionId) = await ResolveAppliedPromotionAsync(cartId, lines, subtotal, cancellationToken);

        var calculation = await _checkoutCalculationService.CalculateEstimatedAsync(checkoutLines, resolvedPromotionId, cancellationToken);

        return new CartDto(
            cartId, itemDtos, availableItems.Sum(i => i.Quantity), subtotal,
            couponCode, promotionName, discount, subtotal - discount,
            calculation.Tax, calculation.TaxRateConfigured,
            calculation.Shipping, calculation.ShippingRateConfigured,
            calculation.GrandTotal);
    }

    private async Task<(List<CartItemDto> ItemDtos, Dictionary<int, Product> Products)> ComputeItemDtosAsync(int cartId, CancellationToken cancellationToken)
    {
        var items = await _dbContext.CartItems
            .Where(ci => ci.CartId == cartId)
            .OrderBy(ci => ci.AddedAtUtc)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return (new List<CartItemDto>(), new Dictionary<int, Product>());
        }

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();

        // IgnoreQueryFilters: a line for a since-soft-deleted product must still
        // display (name/image) so the customer can see what it was and remove
        // it - IsAvailable below is false for it regardless, so only Remove
        // will actually be allowed on it.
        var products = await _dbContext.Products.IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .Include(p => p.Images)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues).ThenInclude(av => av.ProductAttributeValue)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var itemDtos = new List<CartItemDto>();
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                continue;
            }

            var variant = item.ProductVariantId.HasValue
                ? product.Variants.FirstOrDefault(v => v.Id == item.ProductVariantId.Value)
                : null;

            var isAvailable = product.IsActive && product.IsPublished && !product.IsDeleted &&
                (item.ProductVariantId is null || variant is { IsActive: true });

            var sku = variant?.SKU ?? product.BaseSKU;
            var price = _pricingService.Calculate(product.SellingPrice, product.CompareAtPrice, variant?.SellingPrice, variant?.CompareAtPrice);
            var (stockState, available, allowBackorder) = await GetStockAsync(item.ProductId, item.ProductVariantId, product.LowStockThreshold, cancellationToken);
            var variantDescription = variant is null
                ? null
                : string.Join(", ", variant.AttributeValues.Select(av => av.ProductAttributeValue.Value));

            var priceChanged = isAvailable && item.PriceWhenAdded != price.FinalPrice;
            var quantityExceedsStock = isAvailable && !allowBackorder && item.Quantity > available;

            itemDtos.Add(new CartItemDto(
                item.Id,
                item.ProductId,
                item.ProductVariantId,
                product.Name,
                product.Slug,
                BuildImagePath(product, item.ProductVariantId),
                sku,
                variantDescription,
                price.FinalPrice,
                price.CompareAtPrice,
                price.DiscountPercent,
                item.Quantity,
                price.FinalPrice * item.Quantity,
                stockState,
                available,
                isAvailable,
                priceChanged,
                priceChanged ? item.PriceWhenAdded : null,
                quantityExceedsStock));
        }

        return (itemDtos, products);
    }

    private static string? BuildImagePath(Product product, int? variantId)
    {
        var variantImages = variantId.HasValue
            ? product.Images.Where(i => i.ProductVariantId == variantId.Value).ToList()
            : new List<ProductImage>();

        var images = variantImages.Count > 0 ? variantImages : product.Images.Where(i => i.ProductVariantId == null).ToList();

        return images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.Path).FirstOrDefault();
    }

    private async Task<(ProductStockState State, int Available, bool AllowBackorder)> GetStockAsync(
        int productId, int? variantId, int? lowStockThreshold, CancellationToken cancellationToken)
    {
        var inventoryItems = variantId.HasValue
            ? await _dbContext.InventoryItems.Where(i => i.ProductVariantId == variantId.Value).ToListAsync(cancellationToken)
            : await _dbContext.InventoryItems.Where(i => i.ProductId == productId && i.ProductVariantId == null).ToListAsync(cancellationToken);

        if (inventoryItems.Count == 0)
        {
            // Untracked inventory is treated as available - same leniency the
            // product detail page and listing pages already apply.
            return (ProductStockState.InStock, int.MaxValue, true);
        }

        var onHand = inventoryItems.Sum(i => i.QuantityOnHand);
        var reserved = inventoryItems.Sum(i => i.QuantityReserved);
        var available = onHand - reserved;
        var allowBackorder = inventoryItems.Any(i => i.AllowBackorder);

        if (available > (lowStockThreshold ?? 0))
        {
            return (ProductStockState.InStock, available, allowBackorder);
        }

        if (available > 0)
        {
            return (ProductStockState.LowStock, available, allowBackorder);
        }

        return (allowBackorder ? ProductStockState.Backorder : ProductStockState.OutOfStock, available, allowBackorder);
    }
}
