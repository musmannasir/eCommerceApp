using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Catalog;

public sealed class BrandService : IBrandService
{
    private readonly ApplicationDbContext _dbContext;

    public BrandService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<BrandDto>> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);

        if (await _dbContext.Brands.AnyAsync(b => b.Slug == slug, cancellationToken))
        {
            return Result.Failure<BrandDto>(Error.Conflict("brand.duplicate_slug", $"A brand with the slug '{slug}' already exists."));
        }

        var brand = new Brand
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            Website = request.Website,
            IsActive = request.IsActive,
            IsFeatured = request.IsFeatured,
        };

        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(brand));
    }

    public async Task<Result<BrandDto>> UpdateAsync(UpdateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (brand is null)
        {
            return Result.Failure<BrandDto>(Error.NotFound("brand.not_found", "Brand not found."));
        }

        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);
        if (await _dbContext.Brands.AnyAsync(b => b.Slug == slug && b.Id != request.Id, cancellationToken))
        {
            return Result.Failure<BrandDto>(Error.Conflict("brand.duplicate_slug", $"A brand with the slug '{slug}' already exists."));
        }

        brand.Name = request.Name;
        brand.Slug = slug;
        brand.Description = request.Description;
        brand.Website = request.Website;
        brand.IsActive = request.IsActive;
        brand.IsFeatured = request.IsFeatured;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(brand));
    }

    public async Task<Result<BrandDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        return brand is null
            ? Result.Failure<BrandDto>(Error.NotFound("brand.not_found", "Brand not found."))
            : Result.Success(ToDto(brand));
    }

    public async Task<Result<PagedResult<BrandDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var brands = query.OnlyDeleted
            ? _dbContext.Brands.IgnoreQueryFilters().Where(b => b.IsDeleted)
            : _dbContext.Brands.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            brands = brands.Where(b => b.Name.Contains(query.Search) || b.Slug.Contains(query.Search));
        }

        brands = query.SortBy switch
        {
            "Name" => query.SortDescending ? brands.OrderByDescending(b => b.Name) : brands.OrderBy(b => b.Name),
            _ => brands.OrderBy(b => b.Name),
        };

        var totalCount = await brands.CountAsync(cancellationToken);
        var items = await brands
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .Select(b => ToDto(b))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<BrandDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<IReadOnlyList<BrandDto>>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var brands = await _dbContext.Brands
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .AsNoTracking()
            .Select(b => ToDto(b))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BrandDto>>(brands);
    }

    public async Task<Result> SetLogoAsync(int id, string logoPath, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand is null)
        {
            return Result.Failure(Error.NotFound("brand.not_found", "Brand not found."));
        }

        brand.LogoPath = logoPath;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, false, cancellationToken);

    public async Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, true, cancellationToken);

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand is null)
        {
            return Result.Failure(Error.NotFound("brand.not_found", "Brand not found."));
        }

        if (await _dbContext.Products.AnyAsync(p => p.BrandId == id, cancellationToken))
        {
            return Result.Failure(Error.Conflict("brand.has_products", "This brand has products assigned. Reassign them first."));
        }

        _dbContext.Brands.Remove(brand);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand is null)
        {
            return Result.Failure(Error.NotFound("brand.not_found", "Brand not found."));
        }

        brand.IsDeleted = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var brand = await _dbContext.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand is null)
        {
            return Result.Failure(Error.NotFound("brand.not_found", "Brand not found."));
        }

        brand.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static BrandDto ToDto(Brand brand) => new(
        brand.Id, brand.Name, brand.Slug, brand.Description, brand.LogoPath, brand.Website, brand.IsActive, brand.IsFeatured, brand.IsDeleted);
}
