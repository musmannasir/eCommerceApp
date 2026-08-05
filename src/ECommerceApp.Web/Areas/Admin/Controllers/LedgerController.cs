using ECommerceApp.Application.Finance;
using ECommerceApp.Application.Finance.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

/// <summary>
/// The merged charge/refund transaction feed (Milestone 14.1). Gated by
/// CanViewFinancialReports (SuperAdmin/Admin only) rather than
/// CanManageOrders - a detailed transaction listing is more sensitive than
/// the order-management screens OrderManager/CustomerSupport already use,
/// and this policy was already sitting pre-wired in Program.cs since
/// Milestone 1 for exactly this purpose.
/// </summary>
[Area("Admin")]
[Authorize(Policy = Policies.CanViewFinancialReports)]
public class LedgerController : Controller
{
    private readonly IFinanceService _financeService;

    public LedgerController(IFinanceService financeService)
    {
        _financeService = financeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _financeService.GetLedgerAsync(new LedgerQuery { Page = page }, cancellationToken);
        return View(result);
    }

    /// <summary>Day-by-day revenue/refund/net (Milestone 14.2) - defaults to the last 30 days when no range is given.</summary>
    [HttpGet]
    public async Task<IActionResult> CashFlow(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var result = await _financeService.GetCashFlowAsync(new CashFlowQuery { From = from, To = to }, cancellationToken);
        return View(result);
    }

    /// <summary>Downloads the whole ledger, not just the current page (Milestone 14.3).</summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv(CancellationToken cancellationToken = default)
    {
        var entries = await _financeService.GetAllLedgerEntriesAsync(cancellationToken);

        var csv = CsvExport.BuildCsv(
            new[] { "Date", "Type", "Order", "Amount" },
            entries.Select(e => new[] { e.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm"), e.Type.ToString(), e.OrderNumber, e.Amount.ToString("0.00") }));

        return File(csv, "text/csv", "ledger.csv");
    }

    [HttpGet]
    public async Task<IActionResult> CashFlowExportCsv(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var result = await _financeService.GetCashFlowAsync(new CashFlowQuery { From = from, To = to }, cancellationToken);

        var csv = CsvExport.BuildCsv(
            new[] { "Date", "Revenue", "Refunded", "Net" },
            result.Periods.Select(p => new[] { p.Date.ToString("yyyy-MM-dd"), p.Revenue.ToString("0.00"), p.Refunded.ToString("0.00"), p.Net.ToString("0.00") }));

        return File(csv, "text/csv", "cash-flow.csv");
    }
}
