using ECommerceApp.Application.Reporting;
using ECommerceApp.Application.Reporting.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

/// <summary>
/// The admin reports hub (Milestone 14.3) - enables the "Reports" nav
/// placeholder that has sat disabled since Milestone 1. Gated by
/// CanViewFinancialReports, the same policy Ledger/Cash Flow use.
/// </summary>
[Area("Admin")]
[Authorize(Policy = Policies.CanViewFinancialReports)]
public class ReportsController : Controller
{
    private readonly IReportingService _reportingService;

    public ReportsController(IReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> TopSellingProducts(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var result = await _reportingService.GetTopSellingProductsAsync(new TopSellingProductsQuery { From = from, To = to }, cancellationToken);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> TopSellingProductsExportCsv(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var result = await _reportingService.GetTopSellingProductsAsync(new TopSellingProductsQuery { From = from, To = to }, cancellationToken);

        var csv = CsvExport.BuildCsv(
            new[] { "ProductId", "ProductName", "QuantitySold", "Revenue" },
            result.Products.Select(p => new[] { p.ProductId.ToString(), p.ProductName, p.QuantitySold.ToString(), p.Revenue.ToString("0.00") }));

        return File(csv, "text/csv", "top-selling-products.csv");
    }
}
