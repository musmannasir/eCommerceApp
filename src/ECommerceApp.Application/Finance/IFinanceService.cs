using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Finance.Models;

namespace ECommerceApp.Application.Finance;

public interface IFinanceService
{
    /// <summary>All-time revenue/refund/order totals for the admin dashboard (Milestone 14.1).</summary>
    Task<FinancialSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Every successful charge and every refund, newest first, merged into one chronological feed.</summary>
    Task<PagedResult<LedgerEntryDto>> GetLedgerAsync(LedgerQuery query, CancellationToken cancellationToken = default);

    /// <summary>Day-by-day revenue/refund/net across a date range, defaulting to the 30 days ending today (Milestone 14.2).</summary>
    Task<CashFlowResultDto> GetCashFlowAsync(CashFlowQuery query, CancellationToken cancellationToken = default);

    /// <summary>The full ledger feed, unpaginated - for CSV export (Milestone 14.3).</summary>
    Task<IReadOnlyList<LedgerEntryDto>> GetAllLedgerEntriesAsync(CancellationToken cancellationToken = default);
}
