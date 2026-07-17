using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Catalog;

public sealed class ProductAttributeService : IProductAttributeService
{
    private readonly ApplicationDbContext _dbContext;

    public ProductAttributeService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ProductAttributeDto>> CreateAttributeAsync(CreateProductAttributeRequest request, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.ProductAttributes.AnyAsync(a => a.Name == request.Name, cancellationToken))
        {
            return Result.Failure<ProductAttributeDto>(Error.Conflict("attribute.duplicate_name", $"An attribute named '{request.Name}' already exists."));
        }

        var attribute = new ProductAttribute { Name = request.Name };
        _dbContext.ProductAttributes.Add(attribute);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ProductAttributeDto(attribute.Id, attribute.Name, []));
    }

    public async Task<Result<ProductAttributeValueDto>> CreateValueAsync(CreateProductAttributeValueRequest request, CancellationToken cancellationToken = default)
    {
        var attributeExists = await _dbContext.ProductAttributes.AnyAsync(a => a.Id == request.ProductAttributeId, cancellationToken);
        if (!attributeExists)
        {
            return Result.Failure<ProductAttributeValueDto>(Error.NotFound("attribute.not_found", "Attribute not found."));
        }

        var duplicate = await _dbContext.ProductAttributeValues.AnyAsync(
            v => v.ProductAttributeId == request.ProductAttributeId && v.Value == request.Value, cancellationToken);
        if (duplicate)
        {
            return Result.Failure<ProductAttributeValueDto>(Error.Conflict(
                "attribute.duplicate_value", $"The value '{request.Value}' already exists for this attribute."));
        }

        var value = new ProductAttributeValue { ProductAttributeId = request.ProductAttributeId, Value = request.Value };
        _dbContext.ProductAttributeValues.Add(value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ProductAttributeValueDto(value.Id, value.ProductAttributeId, value.Value));
    }

    public async Task<Result<IReadOnlyList<ProductAttributeDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var attributes = await _dbContext.ProductAttributes
            .Include(a => a.Values)
            .OrderBy(a => a.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var items = attributes
            .Select(a => new ProductAttributeDto(
                a.Id,
                a.Name,
                a.Values.OrderBy(v => v.Value).Select(v => new ProductAttributeValueDto(v.Id, v.ProductAttributeId, v.Value)).ToList()))
            .ToList();

        return Result.Success<IReadOnlyList<ProductAttributeDto>>(items);
    }
}
