using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Catalog;

public sealed class ProductService : IProductService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly IClock _clock;

    public ProductService(ApplicationDbContext dbContext, IFileStorage fileStorage, IClock clock)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _clock = clock;
    }

    public async Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateReferencesAsync(request.CategoryId, request.BrandId, cancellationToken);
        if (validation is not null)
        {
            return Result.Failure<ProductDto>(validation);
        }

        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);
        if (await _dbContext.Products.AnyAsync(p => p.Slug == slug, cancellationToken))
        {
            return Result.Failure<ProductDto>(Error.Conflict("product.duplicate_slug", $"A product with the slug '{slug}' already exists."));
        }

        if (await _dbContext.Products.AnyAsync(p => p.BaseSKU == request.BaseSKU, cancellationToken))
        {
            return Result.Failure<ProductDto>(Error.Conflict("product.duplicate_sku", $"A product with SKU '{request.BaseSKU}' already exists."));
        }

        var product = new Product
        {
            Name = request.Name,
            Slug = slug,
            ShortDescription = request.ShortDescription,
            FullDescription = request.FullDescription,
            BrandId = request.BrandId,
            CategoryId = request.CategoryId,
            BaseSKU = request.BaseSKU,
            CostPrice = request.CostPrice,
            SellingPrice = request.SellingPrice,
            CompareAtPrice = request.CompareAtPrice,
            TaxCategory = request.TaxCategory,
            IsTaxable = request.IsTaxable,
            IsActive = request.IsActive,
            IsFeatured = request.IsFeatured,
            Weight = request.Weight,
            Length = request.Length,
            Width = request.Width,
            Height = request.Height,
            WarrantyInformation = request.WarrantyInformation,
            ReturnEligibility = request.ReturnEligibility,
            LowStockThreshold = request.LowStockThreshold,
            SearchKeywords = request.SearchKeywords,
            MetaTitle = request.MetaTitle,
            MetaDescription = request.MetaDescription,
        };

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDtoAsync(product.Id, cancellationToken) ?? throw new InvalidOperationException("Product vanished after insert."));
    }

    public async Task<Result<ProductDto>> UpdateAsync(UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductDto>(Error.NotFound("product.not_found", "Product not found."));
        }

        var validation = await ValidateReferencesAsync(request.CategoryId, request.BrandId, cancellationToken);
        if (validation is not null)
        {
            return Result.Failure<ProductDto>(validation);
        }

        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);
        if (await _dbContext.Products.AnyAsync(p => p.Slug == slug && p.Id != request.Id, cancellationToken))
        {
            return Result.Failure<ProductDto>(Error.Conflict("product.duplicate_slug", $"A product with the slug '{slug}' already exists."));
        }

        if (await _dbContext.Products.AnyAsync(p => p.BaseSKU == request.BaseSKU && p.Id != request.Id, cancellationToken))
        {
            return Result.Failure<ProductDto>(Error.Conflict("product.duplicate_sku", $"A product with SKU '{request.BaseSKU}' already exists."));
        }

        product.Name = request.Name;
        product.Slug = slug;
        product.ShortDescription = request.ShortDescription;
        product.FullDescription = request.FullDescription;
        product.BrandId = request.BrandId;
        product.CategoryId = request.CategoryId;
        product.BaseSKU = request.BaseSKU;
        product.CostPrice = request.CostPrice;
        product.SellingPrice = request.SellingPrice;
        product.CompareAtPrice = request.CompareAtPrice;
        product.TaxCategory = request.TaxCategory;
        product.IsTaxable = request.IsTaxable;
        product.IsActive = request.IsActive;
        product.IsFeatured = request.IsFeatured;
        product.Weight = request.Weight;
        product.Length = request.Length;
        product.Width = request.Width;
        product.Height = request.Height;
        product.WarrantyInformation = request.WarrantyInformation;
        product.ReturnEligibility = request.ReturnEligibility;
        product.LowStockThreshold = request.LowStockThreshold;
        product.SearchKeywords = request.SearchKeywords;
        product.MetaTitle = request.MetaTitle;
        product.MetaDescription = request.MetaDescription;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDtoAsync(product.Id, cancellationToken) ?? throw new InvalidOperationException("Product vanished after update."));
    }

    public async Task<Result<ProductDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var dto = await MapToDtoAsync(id, cancellationToken);
        return dto is null
            ? Result.Failure<ProductDto>(Error.NotFound("product.not_found", "Product not found."))
            : Result.Success(dto);
    }

    public async Task<Result<PagedResult<ProductListItemDto>>> GetPagedAsync(ProductListQuery query, CancellationToken cancellationToken = default)
    {
        var products = query.OnlyDeleted
            ? _dbContext.Products.IgnoreQueryFilters().Where(p => p.IsDeleted).Include(p => p.Brand).Include(p => p.Category)
            : _dbContext.Products.Include(p => p.Brand).Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            products = products.Where(p => p.Name.Contains(query.Search) || p.BaseSKU.Contains(query.Search));
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId);
        }

        if (query.BrandId.HasValue)
        {
            products = products.Where(p => p.BrandId == query.BrandId);
        }

        products = query.SortBy switch
        {
            "Name" => query.SortDescending ? products.OrderByDescending(p => p.Name) : products.OrderBy(p => p.Name),
            "SellingPrice" => query.SortDescending ? products.OrderByDescending(p => p.SellingPrice) : products.OrderBy(p => p.SellingPrice),
            _ => products.OrderByDescending(p => p.CreatedAtUtc),
        };

        var totalCount = await products.CountAsync(cancellationToken);
        var items = await products
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .Select(p => new ProductListItemDto(
                p.Id, p.Name, p.Slug, p.BaseSKU, p.Brand != null ? p.Brand.Name : null, p.Category.Name,
                p.SellingPrice, p.IsActive, p.IsPublished, p.IsFeatured, p.IsDeleted))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ProductListItemDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<IReadOnlyList<ProductPickerItemDto>>> GetPickerListAsync(CancellationToken cancellationToken = default)
    {
        var products = await _dbContext.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .Select(p => new ProductPickerItemDto(
                p.Id,
                p.Name,
                p.BaseSKU,
                p.Variants.Where(v => v.IsActive).Select(v => new ProductVariantPickerItemDto(v.Id, v.SKU)).ToList()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ProductPickerItemDto>>(products);
    }

    public async Task<Result> PublishAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Result.Failure(Error.NotFound("product.not_found", "Product not found."));
        }

        if (!product.IsActive)
        {
            return Result.Failure(Error.Validation("product.inactive", "An inactive product cannot be published."));
        }

        product.IsPublished = true;
        product.PublishedAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UnpublishAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Result.Failure(Error.NotFound("product.not_found", "Product not found."));
        }

        product.IsPublished = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, false, cancellationToken);

    public async Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, true, cancellationToken);

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        // Products are never hard-deleted (see brief section 4) - Remove() on an
        // AuditableEntity is converted to a soft delete by ApplicationDbContext, so this
        // is always safe even once orders reference this product in later milestones.
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Result.Failure(Error.NotFound("product.not_found", "Product not found."));
        }

        _dbContext.Products.Remove(product);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Result.Failure(Error.NotFound("product.not_found", "Product not found."));
        }

        product.IsDeleted = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ProductVariantDto>> AddVariantAsync(CreateVariantRequest request, CancellationToken cancellationToken = default)
    {
        var productExists = await _dbContext.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
        {
            return Result.Failure<ProductVariantDto>(Error.NotFound("product.not_found", "Product not found."));
        }

        if (await _dbContext.ProductVariants.AnyAsync(v => v.SKU == request.SKU, cancellationToken))
        {
            return Result.Failure<ProductVariantDto>(Error.Conflict("variant.duplicate_sku", $"A variant with SKU '{request.SKU}' already exists."));
        }

        var distinctValueIds = request.AttributeValueIds.Distinct().ToList();
        var validValueCount = await _dbContext.ProductAttributeValues.CountAsync(v => distinctValueIds.Contains(v.Id), cancellationToken);
        if (validValueCount != distinctValueIds.Count)
        {
            return Result.Failure<ProductVariantDto>(Error.Validation("variant.invalid_attribute_value", "One or more selected attribute values do not exist."));
        }

        var combinationKey = ProductVariant.BuildCombinationKey(distinctValueIds);
        if (await _dbContext.ProductVariants.AnyAsync(v => v.ProductId == request.ProductId && v.CombinationKey == combinationKey, cancellationToken))
        {
            return Result.Failure<ProductVariantDto>(Error.Conflict("variant.duplicate_combination", "A variant with this exact attribute combination already exists for this product."));
        }

        var variant = new ProductVariant
        {
            ProductId = request.ProductId,
            SKU = request.SKU,
            Barcode = request.Barcode,
            CostPrice = request.CostPrice,
            SellingPrice = request.SellingPrice,
            CompareAtPrice = request.CompareAtPrice,
            Weight = request.Weight,
            IsActive = request.IsActive,
            CombinationKey = combinationKey,
        };
        _dbContext.ProductVariants.Add(variant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var valueId in distinctValueIds)
        {
            _dbContext.ProductVariantAttributeValues.Add(new ProductVariantAttributeValue
            {
                ProductVariantId = variant.Id,
                ProductAttributeValueId = valueId,
            });
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        // MapVariant is a plain C# method, not translatable to SQL - the entity (with its
        // navigations) must be fully materialized via Include() first, then mapped client-side.
        var savedVariant = await _dbContext.ProductVariants
            .Include(v => v.AttributeValues).ThenInclude(av => av.ProductAttributeValue).ThenInclude(pav => pav.ProductAttribute)
            .AsNoTracking()
            .FirstAsync(v => v.Id == variant.Id, cancellationToken);

        return Result.Success(MapVariant(savedVariant));
    }

    public async Task<Result> DeleteVariantAsync(int variantId, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return Result.Failure(Error.NotFound("variant.not_found", "Variant not found."));
        }

        // The ProductImage -> ProductVariant FK is NoAction at the DB level (see
        // ProductImageConfiguration), so detaching is this method's job, not the database's.
        var variantImages = await _dbContext.ProductImages
            .Where(i => i.ProductVariantId == variantId)
            .ToListAsync(cancellationToken);
        foreach (var image in variantImages)
        {
            image.ProductVariantId = null;
        }

        _dbContext.ProductVariants.Remove(variant);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ProductSpecificationDto>> AddSpecificationAsync(CreateSpecificationRequest request, CancellationToken cancellationToken = default)
    {
        var productExists = await _dbContext.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
        {
            return Result.Failure<ProductSpecificationDto>(Error.NotFound("product.not_found", "Product not found."));
        }

        var specification = new ProductSpecification
        {
            ProductId = request.ProductId,
            Name = request.Name,
            Value = request.Value,
            DisplayOrder = request.DisplayOrder,
        };
        _dbContext.ProductSpecifications.Add(specification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ProductSpecificationDto(specification.Id, specification.Name, specification.Value, specification.DisplayOrder));
    }

    public async Task<Result> DeleteSpecificationAsync(int specificationId, CancellationToken cancellationToken = default)
    {
        var specification = await _dbContext.ProductSpecifications.FirstOrDefaultAsync(s => s.Id == specificationId, cancellationToken);
        if (specification is null)
        {
            return Result.Failure(Error.NotFound("specification.not_found", "Specification not found."));
        }

        _dbContext.ProductSpecifications.Remove(specification);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> AddTagAsync(int productId, string tagName, CancellationToken cancellationToken = default)
    {
        var productExists = await _dbContext.Products.AnyAsync(p => p.Id == productId, cancellationToken);
        if (!productExists)
        {
            return Result.Failure(Error.NotFound("product.not_found", "Product not found."));
        }

        var slug = SlugGenerator.Generate(tagName);
        var tag = await _dbContext.ProductTags.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
        if (tag is null)
        {
            tag = new ProductTag { Name = tagName, Slug = slug };
            _dbContext.ProductTags.Add(tag);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var alreadyLinked = await _dbContext.ProductTagMappings.AnyAsync(
            m => m.ProductId == productId && m.ProductTagId == tag.Id, cancellationToken);
        if (alreadyLinked)
        {
            return Result.Failure(Error.Conflict("tag.already_linked", "This tag is already applied to the product."));
        }

        _dbContext.ProductTagMappings.Add(new ProductTagMapping { ProductId = productId, ProductTagId = tag.Id });
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RemoveTagAsync(int productId, int productTagId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.ProductTagMappings.FirstOrDefaultAsync(
            m => m.ProductId == productId && m.ProductTagId == productTagId, cancellationToken);
        if (mapping is null)
        {
            return Result.Failure(Error.NotFound("tag.not_linked", "This tag is not applied to the product."));
        }

        _dbContext.ProductTagMappings.Remove(mapping);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ProductImageDto>> AddImageAsync(
        int productId,
        int? productVariantId,
        Stream content,
        string fileName,
        string contentType,
        string? altText,
        bool isPrimary,
        CancellationToken cancellationToken = default)
    {
        var productExists = await _dbContext.Products.AnyAsync(p => p.Id == productId, cancellationToken);
        if (!productExists)
        {
            return Result.Failure<ProductImageDto>(Error.NotFound("product.not_found", "Product not found."));
        }

        if (productVariantId.HasValue && !await _dbContext.ProductVariants.AnyAsync(v => v.Id == productVariantId && v.ProductId == productId, cancellationToken))
        {
            return Result.Failure<ProductImageDto>(Error.NotFound("variant.not_found", "Variant not found for this product."));
        }

        var saveResult = await _fileStorage.SaveImageAsync(content, fileName, contentType, "products", cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result.Failure<ProductImageDto>(saveResult.FirstError);
        }

        if (isPrimary)
        {
            var existingPrimaries = await _dbContext.ProductImages
                .Where(i => i.ProductId == productId && i.IsPrimary)
                .ToListAsync(cancellationToken);
            foreach (var existing in existingPrimaries)
            {
                existing.IsPrimary = false;
            }
        }

        var nextDisplayOrder = 1 + await _dbContext.ProductImages
            .Where(i => i.ProductId == productId)
            .Select(i => (int?)i.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var image = new ProductImage
        {
            ProductId = productId,
            ProductVariantId = productVariantId,
            Path = saveResult.Value,
            AltText = altText,
            DisplayOrder = nextDisplayOrder,
            IsPrimary = isPrimary,
        };
        _dbContext.ProductImages.Add(image);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ProductImageDto(image.Id, image.ProductVariantId, image.Path, image.AltText, image.DisplayOrder, image.IsPrimary));
    }

    public async Task<Result> DeleteImageAsync(int imageId, CancellationToken cancellationToken = default)
    {
        var image = await _dbContext.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId, cancellationToken);
        if (image is null)
        {
            return Result.Failure(Error.NotFound("image.not_found", "Image not found."));
        }

        _dbContext.ProductImages.Remove(image);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _fileStorage.DeleteAsync(image.Path, cancellationToken);

        return Result.Success();
    }

    private async Task<Error?> ValidateReferencesAsync(int categoryId, int? brandId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            return Error.NotFound("product.category_not_found", "The selected category does not exist.");
        }

        if (brandId.HasValue && !await _dbContext.Brands.AnyAsync(b => b.Id == brandId, cancellationToken))
        {
            return Error.NotFound("product.brand_not_found", "The selected brand does not exist.");
        }

        return null;
    }

    private async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Result.Failure(Error.NotFound("product.not_found", "Product not found."));
        }

        product.IsActive = isActive;
        if (!isActive)
        {
            product.IsPublished = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<ProductDto?> MapToDtoAsync(int productId, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Specifications)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues).ThenInclude(av => av.ProductAttributeValue).ThenInclude(pav => pav.ProductAttribute)
            .Include(p => p.TagMappings).ThenInclude(m => m.ProductTag)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        return new ProductDto(
            product.Id, product.Name, product.Slug, product.ShortDescription, product.FullDescription,
            product.BrandId, product.Brand?.Name, product.CategoryId, product.Category.Name,
            product.BaseSKU, product.CostPrice, product.SellingPrice, product.CompareAtPrice,
            product.TaxCategory, product.IsTaxable, product.IsActive, product.IsFeatured, product.IsPublished, product.PublishedAtUtc,
            product.Weight, product.Length, product.Width, product.Height,
            product.WarrantyInformation, product.ReturnEligibility, product.LowStockThreshold,
            product.SearchKeywords, product.MetaTitle, product.MetaDescription, product.IsDeleted,
            product.Images.OrderBy(i => i.DisplayOrder)
                .Select(i => new ProductImageDto(i.Id, i.ProductVariantId, i.Path, i.AltText, i.DisplayOrder, i.IsPrimary)).ToList(),
            product.Variants.Select(v => MapVariant(v)).ToList(),
            product.Specifications.OrderBy(s => s.DisplayOrder)
                .Select(s => new ProductSpecificationDto(s.Id, s.Name, s.Value, s.DisplayOrder)).ToList(),
            product.TagMappings.Select(m => new ProductTagRefDto(m.ProductTag.Id, m.ProductTag.Name)).ToList());
    }

    private static ProductVariantDto MapVariant(ProductVariant variant) => new(
        variant.Id, variant.SKU, variant.Barcode, variant.CostPrice, variant.SellingPrice, variant.CompareAtPrice, variant.Weight, variant.IsActive,
        variant.AttributeValues
            .Select(av => new ProductVariantAttributeValueDto(
                av.ProductAttributeValue.ProductAttributeId,
                av.ProductAttributeValue.ProductAttribute.Name,
                av.ProductAttributeValueId,
                av.ProductAttributeValue.Value))
            .ToList());
}
