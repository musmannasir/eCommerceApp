using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Reporting;
using ECommerceApp.Application.Reporting.Models;
using ECommerceApp.Domain.Payments;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Reporting;

public sealed class ReportingService : IReportingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;

    public ReportingService(ApplicationDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<TopSellingProductsResultDto> GetTopSellingProductsAsync(TopSellingProductsQuery query, CancellationToken cancellationToken = default)
    {
        var to = (query.To ?? _clock.UtcNow).Date;
        var from = (query.From ?? to.AddDays(-29)).Date;
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var rangeEndExclusive = to.AddDays(1);

        // Materialized then grouped in-memory, the same provider-agnostic
        // approach GetLedgerAsync/GetCashFlowAsync (Milestones 14.1/14.2)
        // already established, rather than relying on GroupBy translating
        // identically across the InMemory and SQL Server providers.
        var lines = await _dbContext.OrderItems
            .Where(oi => oi.Order.Payment != null && oi.Order.Payment.Status == PaymentStatus.Succeeded
                && oi.Order.CreatedAtUtc >= from && oi.Order.CreatedAtUtc < rangeEndExclusive)
            .Select(oi => new { oi.ProductId, oi.ProductName, oi.Quantity, oi.UnitPrice })
            .ToListAsync(cancellationToken);

        var products = lines
            .GroupBy(l => l.ProductId)
            .Select(g => new TopSellingProductDto(g.Key, g.Last().ProductName, g.Sum(x => x.Quantity), g.Sum(x => x.Quantity * x.UnitPrice)))
            .OrderByDescending(p => p.QuantitySold)
            .ThenByDescending(p => p.Revenue)
            .Take(query.Take)
            .ToList();

        return new TopSellingProductsResultDto(from, to, products);
    }
}
