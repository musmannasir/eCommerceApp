using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Configuration;
using ECommerceApp.Application.Taxation;
using ECommerceApp.Application.Taxation.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Taxation;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Taxation;

/// <summary>
/// Admin CRUD mirrors PromotionService's shape. CalculateEstimatedTaxAsync
/// is the only consumer wired up this milestone (CartService's "estimated
/// tax" display) - it reads the store's configured default jurisdiction
/// since there's no real customer destination (Address doesn't exist until
/// Milestone 8.1). CalculateTaxAsync itself is destination-agnostic and
/// ready for Milestone 8's real checkout to call with an actual address.
/// </summary>
public sealed class TaxService : ITaxService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IStoreSettingsService _storeSettingsService;

    public TaxService(ApplicationDbContext dbContext, IStoreSettingsService storeSettingsService)
    {
        _dbContext = dbContext;
        _storeSettingsService = storeSettingsService;
    }

    public async Task<Result<TaxRateDto>> CreateAsync(CreateTaxRateRequest request, CancellationToken cancellationToken = default)
    {
        var conflict = await FindConflictAsync(request.CountryCode, request.RegionCode, request.TaxCategory, null, cancellationToken);
        if (conflict is not null)
        {
            return Result.Failure<TaxRateDto>(ConflictError(conflict));
        }

        var rate = new TaxRate
        {
            CountryCode = request.CountryCode,
            RegionCode = string.IsNullOrWhiteSpace(request.RegionCode) ? null : request.RegionCode,
            TaxCategory = request.TaxCategory,
            RatePercent = request.RatePercent,
            IsActive = request.IsActive,
        };

        _dbContext.TaxRates.Add(rate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(rate));
    }

    public async Task<Result<TaxRateDto>> UpdateAsync(UpdateTaxRateRequest request, CancellationToken cancellationToken = default)
    {
        var rate = await _dbContext.TaxRates.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (rate is null)
        {
            return Result.Failure<TaxRateDto>(Error.NotFound("tax_rate.not_found", "Tax rate not found."));
        }

        var conflict = await FindConflictAsync(request.CountryCode, request.RegionCode, request.TaxCategory, request.Id, cancellationToken);
        if (conflict is not null)
        {
            return Result.Failure<TaxRateDto>(ConflictError(conflict));
        }

        rate.CountryCode = request.CountryCode;
        rate.RegionCode = string.IsNullOrWhiteSpace(request.RegionCode) ? null : request.RegionCode;
        rate.TaxCategory = request.TaxCategory;
        rate.RatePercent = request.RatePercent;
        rate.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(rate));
    }

    public async Task<Result<TaxRateDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var rate = await _dbContext.TaxRates.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return rate is null
            ? Result.Failure<TaxRateDto>(Error.NotFound("tax_rate.not_found", "Tax rate not found."))
            : Result.Success(ToDto(rate));
    }

    public async Task<Result<PagedResult<TaxRateDto>>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var rates = query.OnlyDeleted
            ? _dbContext.TaxRates.IgnoreQueryFilters().Where(r => r.IsDeleted)
            : _dbContext.TaxRates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            rates = rates.Where(r => r.CountryCode.Contains(query.Search) || r.TaxCategory.Contains(query.Search));
        }

        rates = rates.OrderBy(r => r.CountryCode).ThenBy(r => r.RegionCode).ThenBy(r => r.TaxCategory);

        var totalCount = await rates.CountAsync(cancellationToken);
        var items = await rates
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .Select(r => ToDto(r))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<TaxRateDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, true, cancellationToken);

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default) => await SetActiveAsync(id, false, cancellationToken);

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var rate = await _dbContext.TaxRates.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rate is null)
        {
            return Result.Failure(Error.NotFound("tax_rate.not_found", "Tax rate not found."));
        }

        _dbContext.TaxRates.Remove(rate);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var rate = await _dbContext.TaxRates.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rate is null)
        {
            return Result.Failure(Error.NotFound("tax_rate.not_found", "Tax rate not found."));
        }

        rate.IsDeleted = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<TaxCalculationResult> CalculateTaxAsync(
        decimal taxableAmount, string taxCategory, string countryCode, string? regionCode, CancellationToken cancellationToken = default)
    {
        var normalizedCountry = countryCode.ToUpper();
        var normalizedCategory = taxCategory.ToUpper();
        var normalizedRegion = string.IsNullOrWhiteSpace(regionCode) ? null : regionCode.ToUpper();

        TaxRate? rate = null;
        if (normalizedRegion is not null)
        {
            rate = await _dbContext.TaxRates.FirstOrDefaultAsync(r =>
                r.IsActive && r.CountryCode.ToUpper() == normalizedCountry && r.TaxCategory.ToUpper() == normalizedCategory &&
                r.RegionCode != null && r.RegionCode.ToUpper() == normalizedRegion,
                cancellationToken);
        }

        rate ??= await _dbContext.TaxRates.FirstOrDefaultAsync(r =>
            r.IsActive && r.CountryCode.ToUpper() == normalizedCountry && r.TaxCategory.ToUpper() == normalizedCategory && r.RegionCode == null,
            cancellationToken);

        if (rate is null)
        {
            return new TaxCalculationResult(0, 0, false);
        }

        var taxAmount = Math.Round(taxableAmount * (rate.RatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        return new TaxCalculationResult(taxAmount, rate.RatePercent, true);
    }

    public async Task<EstimatedTaxResult> CalculateEstimatedTaxAsync(IReadOnlyList<TaxableLine> lines, CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            return new EstimatedTaxResult(0, false);
        }

        var storeSettings = await _storeSettingsService.GetAsync(cancellationToken);
        var countryCode = storeSettings.DefaultTaxCountryCode;
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return new EstimatedTaxResult(0, false);
        }

        var regionCode = storeSettings.DefaultTaxRegionCode;

        var totalTax = 0m;
        var rateConfigured = false;
        foreach (var line in lines)
        {
            var result = await CalculateTaxAsync(line.Amount, line.TaxCategory, countryCode, regionCode, cancellationToken);
            totalTax += result.TaxAmount;
            rateConfigured |= result.RateConfigured;
        }

        return new EstimatedTaxResult(totalTax, rateConfigured);
    }

    private async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var rate = await _dbContext.TaxRates.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rate is null)
        {
            return Result.Failure(Error.NotFound("tax_rate.not_found", "Tax rate not found."));
        }

        rate.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Looks up a conflicting row via <c>IgnoreQueryFilters()</c> - the
    /// unique indexes backing this country/region/category combination
    /// have no <c>IsDeleted</c> filter (a soft-deleted row still occupies its
    /// natural key at the database level), so a check that only looked at
    /// non-deleted rows would say "no conflict" and then fail with a raw,
    /// unhandled <see cref="DbUpdateException"/> at SaveChanges time for a
    /// combination that matches a previously-deleted rate.
    /// </summary>
    private async Task<TaxRate?> FindConflictAsync(
        string countryCode, string? regionCode, string taxCategory, int? excludingId, CancellationToken cancellationToken)
    {
        var normalizedCountry = countryCode.ToUpper();
        var normalizedCategory = taxCategory.ToUpper();
        var normalizedRegion = string.IsNullOrWhiteSpace(regionCode) ? null : regionCode.ToUpper();

        return await _dbContext.TaxRates.IgnoreQueryFilters().FirstOrDefaultAsync(r =>
            r.CountryCode.ToUpper() == normalizedCountry &&
            r.TaxCategory.ToUpper() == normalizedCategory &&
            (r.RegionCode == null ? normalizedRegion == null : r.RegionCode.ToUpper() == normalizedRegion) &&
            r.Id != excludingId,
            cancellationToken);
    }

    private static Error ConflictError(TaxRate conflict) => conflict.IsDeleted
        ? Error.Conflict("tax_rate.conflict",
            "A rate for this country/region/category combination was deleted - restore it from the Deleted list or choose a different combination.")
        : Error.Conflict("tax_rate.conflict", "A rate already exists for this country/region/category combination.");

    private static TaxRateDto ToDto(TaxRate rate) => new(
        rate.Id, rate.CountryCode, rate.RegionCode, rate.TaxCategory, rate.RatePercent, rate.IsActive, rate.IsDeleted);
}
