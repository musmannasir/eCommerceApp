using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Finance;
using ECommerceApp.Application.Finance.Models;
using ECommerceApp.Domain.Payments;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Finance;

/// <summary>
/// Queries ApplicationDbContext directly, the same convention every other
/// Storefront-adjacent service follows. Payment and Refund are already the
/// immutable ledger rows for money in/out (see their own doc comments) - this
/// service composes them rather than introducing a new, duplicative "Ledger"
/// table, the same way the Inventory "History" view composes StockMovement
/// rows instead of storing a separate summary.
/// </summary>
public sealed class FinanceService : IFinanceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;

    public FinanceService(ApplicationDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<FinancialSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var totalRevenue = await _dbContext.Payments
            .Where(p => p.Status == PaymentStatus.Succeeded)
            .SumAsync(p => p.Amount, cancellationToken);

        var totalRefunded = await _dbContext.Refunds.SumAsync(r => r.Amount, cancellationToken);

        var paidOrderCount = await _dbContext.Payments.CountAsync(p => p.Status == PaymentStatus.Succeeded, cancellationToken);
        var refundCount = await _dbContext.Refunds.CountAsync(cancellationToken);

        var averageOrderValue = paidOrderCount == 0 ? 0m : totalRevenue / paidOrderCount;

        return new FinancialSummaryDto(totalRevenue, totalRefunded, totalRevenue - totalRefunded, paidOrderCount, refundCount, averageOrderValue);
    }

    public async Task<PagedResult<LedgerEntryDto>> GetLedgerAsync(LedgerQuery query, CancellationToken cancellationToken = default)
    {
        var combined = await BuildLedgerFeedAsync(cancellationToken);

        var page = combined
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<LedgerEntryDto>(page, combined.Count, query.Page, query.PageSize);
    }

    /// <summary>The full, unpaginated feed - for CSV export (Milestone 14.3), which downloads the whole ledger rather than one page of it.</summary>
    public async Task<IReadOnlyList<LedgerEntryDto>> GetAllLedgerEntriesAsync(CancellationToken cancellationToken = default) =>
        await BuildLedgerFeedAsync(cancellationToken);

    private async Task<List<LedgerEntryDto>> BuildLedgerFeedAsync(CancellationToken cancellationToken)
    {
        // Merged client-side rather than via a single SQL UNION query - this
        // app's data volume doesn't warrant the extra complexity of a
        // provider-translated Concat, and it keeps behavior identical
        // between the InMemory (unit test) and SQL Server (real/integration)
        // providers.
        var charges = await _dbContext.Payments
            .Where(p => p.Status == PaymentStatus.Succeeded)
            .Select(p => new LedgerEntryDto(LedgerEntryType.Charge, p.Order.OrderNumber, p.Amount, p.ProcessedAtUtc))
            .ToListAsync(cancellationToken);

        var refunds = await _dbContext.Refunds
            .Select(r => new LedgerEntryDto(LedgerEntryType.Refund, r.Order.OrderNumber, -r.Amount, r.ProcessedAtUtc))
            .ToListAsync(cancellationToken);

        return charges.Concat(refunds)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ToList();
    }

    public async Task<CashFlowResultDto> GetCashFlowAsync(CashFlowQuery query, CancellationToken cancellationToken = default)
    {
        var to = (query.To ?? _clock.UtcNow).Date;
        var from = (query.From ?? to.AddDays(-29)).Date;
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var rangeEndExclusive = to.AddDays(1);

        var charges = await _dbContext.Payments
            .Where(p => p.Status == PaymentStatus.Succeeded && p.ProcessedAtUtc >= from && p.ProcessedAtUtc < rangeEndExclusive)
            .Select(p => new { p.Amount, p.ProcessedAtUtc })
            .ToListAsync(cancellationToken);

        var refunds = await _dbContext.Refunds
            .Where(r => r.ProcessedAtUtc >= from && r.ProcessedAtUtc < rangeEndExclusive)
            .Select(r => new { r.Amount, r.ProcessedAtUtc })
            .ToListAsync(cancellationToken);

        var revenueByDay = charges.GroupBy(c => c.ProcessedAtUtc.Date).ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));
        var refundedByDay = refunds.GroupBy(r => r.ProcessedAtUtc.Date).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

        var periods = new List<CashFlowPeriodDto>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var revenue = revenueByDay.GetValueOrDefault(day);
            var refunded = refundedByDay.GetValueOrDefault(day);
            periods.Add(new CashFlowPeriodDto(day, revenue, refunded, revenue - refunded));
        }

        return new CashFlowResultDto(
            from, to, periods.Sum(p => p.Revenue), periods.Sum(p => p.Refunded), periods.Sum(p => p.Net), periods);
    }
}
