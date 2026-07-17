using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Catalog;

public sealed class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _dbContext;

    public CategoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);

        if (await _dbContext.Categories.AnyAsync(c => c.Slug == slug, cancellationToken))
        {
            return Result.Failure<CategoryDto>(Error.Conflict("category.duplicate_slug", $"A category with the slug '{slug}' already exists."));
        }

        if (request.ParentCategoryId.HasValue &&
            !await _dbContext.Categories.AnyAsync(c => c.Id == request.ParentCategoryId, cancellationToken))
        {
            return Result.Failure<CategoryDto>(Error.NotFound("category.parent_not_found", "The selected parent category does not exist."));
        }

        var category = new Category
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            ParentCategoryId = request.ParentCategoryId,
            DisplayOrder = request.DisplayOrder,
            ImagePath = request.ImagePath,
            IsActive = request.IsActive,
            IsFeatured = request.IsFeatured,
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDtoAsync(category, cancellationToken));
    }

    public async Task<Result<CategoryDto>> UpdateAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<CategoryDto>(Error.NotFound("category.not_found", "Category not found."));
        }

        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);
        if (await _dbContext.Categories.AnyAsync(c => c.Slug == slug && c.Id != request.Id, cancellationToken))
        {
            return Result.Failure<CategoryDto>(Error.Conflict("category.duplicate_slug", $"A category with the slug '{slug}' already exists."));
        }

        if (request.ParentCategoryId.HasValue)
        {
            if (request.ParentCategoryId == request.Id)
            {
                return Result.Failure<CategoryDto>(Error.Validation("category.circular_reference", "A category cannot be its own parent."));
            }

            if (!await _dbContext.Categories.AnyAsync(c => c.Id == request.ParentCategoryId, cancellationToken))
            {
                return Result.Failure<CategoryDto>(Error.NotFound("category.parent_not_found", "The selected parent category does not exist."));
            }

            if (await WouldCreateCycleAsync(request.Id, request.ParentCategoryId.Value, cancellationToken))
            {
                return Result.Failure<CategoryDto>(Error.Validation(
                    "category.circular_reference",
                    "A category cannot be moved under one of its own subcategories."));
            }
        }

        category.Name = request.Name;
        category.Slug = slug;
        category.Description = request.Description;
        category.ParentCategoryId = request.ParentCategoryId;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        category.IsFeatured = request.IsFeatured;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDtoAsync(category, cancellationToken));
    }

    public async Task<Result<CategoryDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return category is null
            ? Result.Failure<CategoryDto>(Error.NotFound("category.not_found", "Category not found."))
            : Result.Success(await MapToDtoAsync(category, cancellationToken));
    }

    public async Task<Result<PagedResult<CategoryDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var categories = query.OnlyDeleted
            ? _dbContext.Categories.IgnoreQueryFilters().Where(c => c.IsDeleted)
            : _dbContext.Categories.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            categories = categories.Where(c => c.Name.Contains(query.Search) || c.Slug.Contains(query.Search));
        }

        categories = query.SortBy switch
        {
            "Name" => query.SortDescending ? categories.OrderByDescending(c => c.Name) : categories.OrderBy(c => c.Name),
            "DisplayOrder" => query.SortDescending ? categories.OrderByDescending(c => c.DisplayOrder) : categories.OrderBy(c => c.DisplayOrder),
            _ => categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name),
        };

        var totalCount = await categories.CountAsync(cancellationToken);
        var page = await categories
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var parentNames = await _dbContext.Categories
            .Where(c => page.Select(p => p.ParentCategoryId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var items = page.Select(c => ToDto(c, c.ParentCategoryId.HasValue ? parentNames.GetValueOrDefault(c.ParentCategoryId.Value) : null)).ToList();

        return Result.Success(new PagedResult<CategoryDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<IReadOnlyList<CategoryTreeNodeDto>>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var all = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        IReadOnlyList<CategoryTreeNodeDto> BuildChildren(int? parentId) => all
            .Where(c => c.ParentCategoryId == parentId)
            .Select(c => new CategoryTreeNodeDto(c.Id, c.Name, c.Slug, c.DisplayOrder, c.IsActive, BuildChildren(c.Id)))
            .ToList();

        return Result.Success(BuildChildren(null));
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _dbContext.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var byId = categories.ToDictionary(c => c.Id, c => c.Name);
        var items = categories
            .Select(c => ToDto(c, c.ParentCategoryId.HasValue ? byId.GetValueOrDefault(c.ParentCategoryId.Value) : null))
            .ToList();

        return Result.Success<IReadOnlyList<CategoryDto>>(items);
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, false, cancellationToken);

    public async Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, true, cancellationToken);

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(Error.NotFound("category.not_found", "Category not found."));
        }

        if (await _dbContext.Categories.AnyAsync(c => c.ParentCategoryId == id, cancellationToken))
        {
            return Result.Failure(Error.Conflict("category.has_children", "This category has subcategories. Reassign or delete them first."));
        }

        if (await _dbContext.Products.AnyAsync(p => p.CategoryId == id, cancellationToken))
        {
            return Result.Failure(Error.Conflict("category.has_products", "This category has products assigned. Reassign them first."));
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(Error.NotFound("category.not_found", "Category not found."));
        }

        category.IsDeleted = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(Error.NotFound("category.not_found", "Category not found."));
        }

        category.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<bool> WouldCreateCycleAsync(int categoryId, int proposedParentId, CancellationToken cancellationToken)
    {
        var currentId = (int?)proposedParentId;
        var guard = 0;

        while (currentId.HasValue && guard++ < 1000)
        {
            if (currentId.Value == categoryId)
            {
                return true;
            }

            currentId = await _dbContext.Categories
                .Where(c => c.Id == currentId.Value)
                .Select(c => c.ParentCategoryId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    private async Task<CategoryDto> MapToDtoAsync(Category category, CancellationToken cancellationToken)
    {
        string? parentName = null;
        if (category.ParentCategoryId.HasValue)
        {
            parentName = await _dbContext.Categories
                .Where(c => c.Id == category.ParentCategoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return ToDto(category, parentName);
    }

    private static CategoryDto ToDto(Category category, string? parentName) => new(
        category.Id,
        category.Name,
        category.Slug,
        category.Description,
        category.ParentCategoryId,
        parentName,
        category.DisplayOrder,
        category.ImagePath,
        category.IsActive,
        category.IsFeatured,
        category.IsDeleted);
}
