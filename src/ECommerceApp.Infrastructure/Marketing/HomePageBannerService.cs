using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Marketing;
using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Marketing;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Marketing;

public sealed class HomePageBannerService : IHomePageBannerService
{
    private readonly ApplicationDbContext _dbContext;

    public HomePageBannerService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<HomePageBannerDto>> CreateAsync(CreateHomePageBannerRequest request, CancellationToken cancellationToken = default)
    {
        var banner = new HomePageBanner
        {
            Title = request.Title,
            Subtitle = request.Subtitle,
            LinkUrl = request.LinkUrl,
            BannerType = Enum.Parse<BannerType>(request.BannerType),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        _dbContext.HomePageBanners.Add(banner);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(banner));
    }

    public async Task<Result<HomePageBannerDto>> UpdateAsync(UpdateHomePageBannerRequest request, CancellationToken cancellationToken = default)
    {
        var banner = await _dbContext.HomePageBanners.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (banner is null)
        {
            return Result.Failure<HomePageBannerDto>(Error.NotFound("home_banner.not_found", "Banner not found."));
        }

        banner.Title = request.Title;
        banner.Subtitle = request.Subtitle;
        banner.LinkUrl = request.LinkUrl;
        banner.BannerType = Enum.Parse<BannerType>(request.BannerType);
        banner.DisplayOrder = request.DisplayOrder;
        banner.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(banner));
    }

    public async Task<Result<HomePageBannerDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var banner = await _dbContext.HomePageBanners.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        return banner is null
            ? Result.Failure<HomePageBannerDto>(Error.NotFound("home_banner.not_found", "Banner not found."))
            : Result.Success(ToDto(banner));
    }

    public async Task<Result<PagedResult<HomePageBannerDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var banners = query.OnlyDeleted
            ? _dbContext.HomePageBanners.IgnoreQueryFilters().Where(b => b.IsDeleted)
            : _dbContext.HomePageBanners.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            banners = banners.Where(b => b.Title.Contains(query.Search));
        }

        banners = banners.OrderBy(b => b.BannerType).ThenBy(b => b.DisplayOrder);

        var totalCount = await banners.CountAsync(cancellationToken);
        var items = await banners
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .Select(b => ToDto(b))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<HomePageBannerDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result> SetImageAsync(int id, string imagePath, CancellationToken cancellationToken = default)
    {
        var banner = await _dbContext.HomePageBanners.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (banner is null)
        {
            return Result.Failure(Error.NotFound("home_banner.not_found", "Banner not found."));
        }

        banner.ImagePath = imagePath;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, false, cancellationToken);

    public async Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, true, cancellationToken);

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var banner = await _dbContext.HomePageBanners.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (banner is null)
        {
            return Result.Failure(Error.NotFound("home_banner.not_found", "Banner not found."));
        }

        _dbContext.HomePageBanners.Remove(banner);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var banner = await _dbContext.HomePageBanners.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (banner is null)
        {
            return Result.Failure(Error.NotFound("home_banner.not_found", "Banner not found."));
        }

        banner.IsDeleted = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var banner = await _dbContext.HomePageBanners.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (banner is null)
        {
            return Result.Failure(Error.NotFound("home_banner.not_found", "Banner not found."));
        }

        banner.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static HomePageBannerDto ToDto(HomePageBanner banner) => new(
        banner.Id, banner.Title, banner.Subtitle, banner.ImagePath, banner.LinkUrl,
        banner.BannerType.ToString(), banner.DisplayOrder, banner.IsActive, banner.IsDeleted);
}
