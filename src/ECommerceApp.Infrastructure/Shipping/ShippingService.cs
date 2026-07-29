using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Shipping;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Shipping;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECommerceApp.Infrastructure.Shipping;

/// <summary>
/// Admin CRUD mirrors TaxService's shape. CalculateEstimatedShippingAsync is
/// the only consumer wired up this milestone (CartService's "estimated
/// shipping" display) - it reads the store's configured default
/// jurisdiction since there's no real customer destination (Address doesn't
/// exist until Milestone 8.1) and no method-picker UI yet (Milestone 8.2).
/// GetAvailableShippingOptionsAsync itself is destination-agnostic and
/// ready for both once they exist.
/// </summary>
public sealed class ShippingService : IShippingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public ShippingService(ApplicationDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public async Task<Result<ShippingMethodDto>> CreateAsync(CreateShippingMethodRequest request, CancellationToken cancellationToken = default)
    {
        var conflict = await FindConflictAsync(request.CountryCode, request.RegionCode, request.Name, null, cancellationToken);
        if (conflict is not null)
        {
            return Result.Failure<ShippingMethodDto>(ConflictError(conflict));
        }

        var method = new ShippingMethod
        {
            Name = request.Name,
            Description = request.Description,
            CountryCode = request.CountryCode,
            RegionCode = string.IsNullOrWhiteSpace(request.RegionCode) ? null : request.RegionCode,
            BaseRate = request.BaseRate,
            RatePerKg = request.RatePerKg,
            FreeShippingThreshold = request.FreeShippingThreshold,
            EstimatedDeliveryDaysMin = request.EstimatedDeliveryDaysMin,
            EstimatedDeliveryDaysMax = request.EstimatedDeliveryDaysMax,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        _dbContext.ShippingMethods.Add(method);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(method));
    }

    public async Task<Result<ShippingMethodDto>> UpdateAsync(UpdateShippingMethodRequest request, CancellationToken cancellationToken = default)
    {
        var method = await _dbContext.ShippingMethods.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);
        if (method is null)
        {
            return Result.Failure<ShippingMethodDto>(Error.NotFound("shipping_method.not_found", "Shipping method not found."));
        }

        var conflict = await FindConflictAsync(request.CountryCode, request.RegionCode, request.Name, request.Id, cancellationToken);
        if (conflict is not null)
        {
            return Result.Failure<ShippingMethodDto>(ConflictError(conflict));
        }

        method.Name = request.Name;
        method.Description = request.Description;
        method.CountryCode = request.CountryCode;
        method.RegionCode = string.IsNullOrWhiteSpace(request.RegionCode) ? null : request.RegionCode;
        method.BaseRate = request.BaseRate;
        method.RatePerKg = request.RatePerKg;
        method.FreeShippingThreshold = request.FreeShippingThreshold;
        method.EstimatedDeliveryDaysMin = request.EstimatedDeliveryDaysMin;
        method.EstimatedDeliveryDaysMax = request.EstimatedDeliveryDaysMax;
        method.DisplayOrder = request.DisplayOrder;
        method.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(method));
    }

    public async Task<Result<ShippingMethodDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var method = await _dbContext.ShippingMethods.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        return method is null
            ? Result.Failure<ShippingMethodDto>(Error.NotFound("shipping_method.not_found", "Shipping method not found."))
            : Result.Success(ToDto(method));
    }

    public async Task<Result<PagedResult<ShippingMethodDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var methods = query.OnlyDeleted
            ? _dbContext.ShippingMethods.IgnoreQueryFilters().Where(m => m.IsDeleted)
            : _dbContext.ShippingMethods.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            methods = methods.Where(m => m.Name.Contains(query.Search) || m.CountryCode.Contains(query.Search));
        }

        methods = methods.OrderBy(m => m.CountryCode).ThenBy(m => m.RegionCode).ThenBy(m => m.DisplayOrder);

        var totalCount = await methods.CountAsync(cancellationToken);
        var items = await methods
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .Select(m => ToDto(m))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ShippingMethodDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, true, cancellationToken);

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, false, cancellationToken);

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var method = await _dbContext.ShippingMethods.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (method is null)
        {
            return Result.Failure(Error.NotFound("shipping_method.not_found", "Shipping method not found."));
        }

        _dbContext.ShippingMethods.Remove(method);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var method = await _dbContext.ShippingMethods.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (method is null)
        {
            return Result.Failure(Error.NotFound("shipping_method.not_found", "Shipping method not found."));
        }

        method.IsDeleted = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<ShippingOptionDto>> GetAvailableShippingOptionsAsync(
        decimal totalWeightKg, decimal subtotal, string countryCode, string? regionCode, CancellationToken cancellationToken = default)
    {
        var normalizedCountry = countryCode.ToUpper();
        var normalizedRegion = string.IsNullOrWhiteSpace(regionCode) ? null : regionCode.ToUpper();

        var methods = await _dbContext.ShippingMethods
            .Where(m => m.IsActive && m.CountryCode.ToUpper() == normalizedCountry &&
                (m.RegionCode == null || (normalizedRegion != null && m.RegionCode.ToUpper() == normalizedRegion)))
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(cancellationToken);

        return methods.Select(m =>
        {
            var freeShipping = m.FreeShippingThreshold.HasValue && subtotal >= m.FreeShippingThreshold.Value;
            var cost = freeShipping ? 0m : m.BaseRate + (m.RatePerKg * totalWeightKg);
            return new ShippingOptionDto(m.Id, m.Name, m.Description, cost, m.EstimatedDeliveryDaysMin, m.EstimatedDeliveryDaysMax);
        }).ToList();
    }

    public async Task<EstimatedShippingResult> CalculateEstimatedShippingAsync(
        decimal totalWeightKg, decimal subtotal, CancellationToken cancellationToken = default)
    {
        var countryCode = _configuration["Store:DefaultShippingCountryCode"];
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return new EstimatedShippingResult(0, false);
        }

        var regionCode = _configuration["Store:DefaultShippingRegionCode"];
        var options = await GetAvailableShippingOptionsAsync(totalWeightKg, subtotal, countryCode, regionCode, cancellationToken);

        if (options.Count == 0)
        {
            return new EstimatedShippingResult(0, false);
        }

        return new EstimatedShippingResult(options.Min(o => o.Cost), true);
    }

    /// <summary>
    /// Looks up a conflicting row via <c>IgnoreQueryFilters()</c> - the
    /// unique indexes backing this name/jurisdiction combination have
    /// no <c>IsDeleted</c> filter (a soft-deleted row still occupies its
    /// natural key at the database level), so a check that only looked at
    /// non-deleted rows would say "no conflict" and then fail with a raw,
    /// unhandled <see cref="DbUpdateException"/> at SaveChanges time for a
    /// name that matches a previously-deleted method.
    /// </summary>
    private async Task<ShippingMethod?> FindConflictAsync(string countryCode, string? regionCode, string name, int? excludingId, CancellationToken cancellationToken)
    {
        var normalizedCountry = countryCode.ToUpper();
        var normalizedName = name.ToUpper();
        var normalizedRegion = string.IsNullOrWhiteSpace(regionCode) ? null : regionCode.ToUpper();

        return await _dbContext.ShippingMethods.IgnoreQueryFilters().FirstOrDefaultAsync(m =>
            m.CountryCode.ToUpper() == normalizedCountry &&
            m.Name.ToUpper() == normalizedName &&
            (m.RegionCode == null ? normalizedRegion == null : m.RegionCode.ToUpper() == normalizedRegion) &&
            m.Id != excludingId,
            cancellationToken);
    }

    private static Error ConflictError(ShippingMethod conflict) => conflict.IsDeleted
        ? Error.Conflict("shipping_method.conflict",
            "A method with this name for this country/region was deleted - restore it from the Deleted list or choose a different name.")
        : Error.Conflict("shipping_method.conflict", "A method with this name already exists for this country/region.");

    private async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var method = await _dbContext.ShippingMethods.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (method is null)
        {
            return Result.Failure(Error.NotFound("shipping_method.not_found", "Shipping method not found."));
        }

        method.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static ShippingMethodDto ToDto(ShippingMethod method) => new(
        method.Id, method.Name, method.Description, method.CountryCode, method.RegionCode,
        method.BaseRate, method.RatePerKg, method.FreeShippingThreshold,
        method.EstimatedDeliveryDaysMin, method.EstimatedDeliveryDaysMax,
        method.DisplayOrder, method.IsActive, method.IsDeleted);
}
