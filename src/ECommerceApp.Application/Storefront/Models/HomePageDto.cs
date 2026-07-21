namespace ECommerceApp.Application.Storefront.Models;

public record HomeBannerDto(int Id, string Title, string? Subtitle, string? ImagePath, string? LinkUrl);

public record HomeCategoryCardDto(int Id, string Name, string Slug, string? ImagePath);

public record HomeProductCardDto(
    int Id,
    string Name,
    string Slug,
    string? ImagePath,
    string? BrandName,
    string? BrandSlug,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    bool IsOutOfStock)
{
    public int? DiscountPercent => CompareAtPrice.HasValue && CompareAtPrice.Value > SellingPrice
        ? (int)Math.Round((1 - SellingPrice / CompareAtPrice.Value) * 100)
        : null;
}

public record HomePageDto(
    IReadOnlyList<HomeBannerDto> HeroBanners,
    IReadOnlyList<HomeBannerDto> PromoBanners,
    IReadOnlyList<HomeCategoryCardDto> FeaturedCategories,
    IReadOnlyList<HomeProductCardDto> FeaturedProducts,
    IReadOnlyList<HomeProductCardDto> NewArrivals,
    IReadOnlyList<HomeProductCardDto> DiscountedProducts);
