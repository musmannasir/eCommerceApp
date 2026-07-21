using ECommerceApp.Application.Storefront.Models;

namespace ECommerceApp.Application.Wishlist.Models;

public record WishlistItemDto(int Id, HomeProductCardDto Product, DateTime AddedAtUtc);

public record WishlistDto(IReadOnlyList<WishlistItemDto> Items);

public record WishlistToggleResultDto(bool IsWishlisted, int ItemCount);
