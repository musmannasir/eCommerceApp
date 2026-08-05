using ECommerceApp.Application.Common.Models;

namespace ECommerceApp.Application.Finance.Models;

/// <summary>All-time totals only (Milestone 14.1) - see CashFlowQuery/GetCashFlowAsync (Milestone 14.2) for date-ranged, day-by-day breakdowns.</summary>
public record FinancialSummaryDto(
    decimal TotalRevenue,
    decimal TotalRefunded,
    decimal NetRevenue,
    int PaidOrderCount,
    int RefundCount,
    decimal AverageOrderValue);

public enum LedgerEntryType
{
    Charge,
    Refund,
}

/// <summary>
/// One row in the merged Payment+Refund feed. Amount is signed the same way
/// StockMovement.QuantityChange already is - a Charge is positive, a Refund
/// is negative - so a reader can eyeball a running total without needing to
/// branch on Type first.
/// </summary>
public record LedgerEntryDto(LedgerEntryType Type, string OrderNumber, decimal Amount, DateTime OccurredAtUtc);

public record LedgerQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// From/To are inclusive whole days; either or both may be omitted, in
/// which case GetCashFlowAsync defaults to the 30 days ending today. Daily
/// granularity only - there's no existing signal in this app for a
/// week/month toggle, so one wasn't speculatively built.
/// </summary>
public record CashFlowQuery
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

/// <summary>One calendar day's revenue/refund/net - every day in the requested range appears, including days with no activity at all, so a reader sees a continuous timeline rather than gaps.</summary>
public record CashFlowPeriodDto(DateTime Date, decimal Revenue, decimal Refunded, decimal Net);

public record CashFlowResultDto(
    DateTime From,
    DateTime To,
    decimal TotalRevenue,
    decimal TotalRefunded,
    decimal TotalNet,
    IReadOnlyList<CashFlowPeriodDto> Periods);
