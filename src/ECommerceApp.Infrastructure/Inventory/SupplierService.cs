using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Inventory;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Inventory;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Inventory;

public sealed class SupplierService : ISupplierService
{
    private readonly ApplicationDbContext _dbContext;

    public SupplierService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<SupplierDto>> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.Suppliers.AnyAsync(s => s.Code == request.Code, cancellationToken))
        {
            return Result.Failure<SupplierDto>(Error.Conflict("supplier.duplicate_code", $"A supplier with the code '{request.Code}' already exists."));
        }

        var supplier = new Supplier
        {
            Name = request.Name,
            Code = request.Code,
            ContactName = request.ContactName,
            Email = request.Email,
            Phone = request.Phone,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            Region = request.Region,
            PostalCode = request.PostalCode,
            Country = request.Country,
            Website = request.Website,
            Notes = request.Notes,
            IsActive = request.IsActive,
        };

        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(supplier));
    }

    public async Task<Result<SupplierDto>> UpdateAsync(UpdateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<SupplierDto>(Error.NotFound("supplier.not_found", "Supplier not found."));
        }

        if (await _dbContext.Suppliers.AnyAsync(s => s.Code == request.Code && s.Id != request.Id, cancellationToken))
        {
            return Result.Failure<SupplierDto>(Error.Conflict("supplier.duplicate_code", $"A supplier with the code '{request.Code}' already exists."));
        }

        supplier.Name = request.Name;
        supplier.Code = request.Code;
        supplier.ContactName = request.ContactName;
        supplier.Email = request.Email;
        supplier.Phone = request.Phone;
        supplier.AddressLine1 = request.AddressLine1;
        supplier.AddressLine2 = request.AddressLine2;
        supplier.City = request.City;
        supplier.Region = request.Region;
        supplier.PostalCode = request.PostalCode;
        supplier.Country = request.Country;
        supplier.Website = request.Website;
        supplier.Notes = request.Notes;
        supplier.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(supplier));
    }

    public async Task<Result<SupplierDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return supplier is null
            ? Result.Failure<SupplierDto>(Error.NotFound("supplier.not_found", "Supplier not found."))
            : Result.Success(ToDto(supplier));
    }

    public async Task<Result<PagedResult<SupplierDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var suppliers = query.OnlyDeleted
            ? _dbContext.Suppliers.IgnoreQueryFilters().Where(s => s.IsDeleted)
            : _dbContext.Suppliers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            suppliers = suppliers.Where(s => s.Name.Contains(query.Search) || s.Code.Contains(query.Search));
        }

        suppliers = query.SortBy switch
        {
            "Name" => query.SortDescending ? suppliers.OrderByDescending(s => s.Name) : suppliers.OrderBy(s => s.Name),
            _ => suppliers.OrderBy(s => s.Name),
        };

        var totalCount = await suppliers.CountAsync(cancellationToken);
        var items = await suppliers
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .Select(s => ToDto(s))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<SupplierDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<IReadOnlyList<SupplierDto>>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var suppliers = await _dbContext.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .Select(s => ToDto(s))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SupplierDto>>(suppliers);
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, false, cancellationToken);

    public async Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, true, cancellationToken);

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(Error.NotFound("supplier.not_found", "Supplier not found."));
        }

        _dbContext.Suppliers.Remove(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(Error.NotFound("supplier.not_found", "Supplier not found."));
        }

        supplier.IsDeleted = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<SupplierProductDto>>> GetLinkedProductsAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        var links = await _dbContext.SupplierProducts
            .Where(sp => sp.SupplierId == supplierId)
            .Include(sp => sp.Product)
            .OrderBy(sp => sp.Product.Name)
            .AsNoTracking()
            .Select(sp => ToDto(sp))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SupplierProductDto>>(links);
    }

    public async Task<Result<SupplierProductDto>> LinkProductAsync(LinkSupplierProductRequest request, CancellationToken cancellationToken = default)
    {
        var supplierExists = await _dbContext.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (!supplierExists)
        {
            return Result.Failure<SupplierProductDto>(Error.NotFound("supplier.not_found", "Supplier not found."));
        }

        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<SupplierProductDto>(Error.NotFound("product.not_found", "Product not found."));
        }

        if (await _dbContext.SupplierProducts.AnyAsync(
            sp => sp.SupplierId == request.SupplierId && sp.ProductId == request.ProductId, cancellationToken))
        {
            return Result.Failure<SupplierProductDto>(Error.Conflict("supplier_product.already_linked", "This product is already linked to the supplier."));
        }

        var link = new SupplierProduct
        {
            SupplierId = request.SupplierId,
            ProductId = request.ProductId,
            SupplierSku = request.SupplierSku,
            CostPrice = request.CostPrice,
            LeadTimeDays = request.LeadTimeDays,
            IsPreferred = request.IsPreferred,
        };

        _dbContext.SupplierProducts.Add(link);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new SupplierProductDto(
            link.Id, link.SupplierId, link.ProductId, product.Name, product.BaseSKU,
            link.SupplierSku, link.CostPrice, link.LeadTimeDays, link.IsPreferred));
    }

    public async Task<Result<SupplierProductDto>> UpdateProductLinkAsync(UpdateSupplierProductRequest request, CancellationToken cancellationToken = default)
    {
        var link = await _dbContext.SupplierProducts
            .Include(sp => sp.Product)
            .FirstOrDefaultAsync(sp => sp.Id == request.Id, cancellationToken);
        if (link is null)
        {
            return Result.Failure<SupplierProductDto>(Error.NotFound("supplier_product.not_found", "This supplier-product link was not found."));
        }

        link.SupplierSku = request.SupplierSku;
        link.CostPrice = request.CostPrice;
        link.LeadTimeDays = request.LeadTimeDays;
        link.IsPreferred = request.IsPreferred;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(link));
    }

    public async Task<Result> UnlinkProductAsync(int supplierProductId, CancellationToken cancellationToken = default)
    {
        var link = await _dbContext.SupplierProducts.FirstOrDefaultAsync(sp => sp.Id == supplierProductId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(Error.NotFound("supplier_product.not_found", "This supplier-product link was not found."));
        }

        _dbContext.SupplierProducts.Remove(link);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(Error.NotFound("supplier.not_found", "Supplier not found."));
        }

        supplier.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static SupplierDto ToDto(Supplier supplier) => new(
        supplier.Id, supplier.Name, supplier.Code, supplier.ContactName, supplier.Email, supplier.Phone,
        supplier.AddressLine1, supplier.AddressLine2, supplier.City, supplier.Region, supplier.PostalCode, supplier.Country,
        supplier.Website, supplier.Notes, supplier.IsActive, supplier.IsDeleted);

    private static SupplierProductDto ToDto(SupplierProduct link) => new(
        link.Id, link.SupplierId, link.ProductId, link.Product.Name, link.Product.BaseSKU,
        link.SupplierSku, link.CostPrice, link.LeadTimeDays, link.IsPreferred);
}
