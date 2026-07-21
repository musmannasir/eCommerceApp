namespace ECommerceApp.Application.Marketing.Models;

public record HomePageBannerDto(
    int Id,
    string Title,
    string? Subtitle,
    string? ImagePath,
    string? LinkUrl,
    string BannerType,
    int DisplayOrder,
    bool IsActive,
    bool IsDeleted);

public record CreateHomePageBannerRequest(
    string Title,
    string? Subtitle,
    string? LinkUrl,
    string BannerType,
    int DisplayOrder,
    bool IsActive);

public record UpdateHomePageBannerRequest(
    int Id,
    string Title,
    string? Subtitle,
    string? LinkUrl,
    string BannerType,
    int DisplayOrder,
    bool IsActive);
