using ECommerceApp.Application.Reporting.Models;

namespace ECommerceApp.Application.Reporting;

/// <summary>
/// Cross-cutting admin reports (Milestone 14.3) - kept separate from
/// IFinanceService, which is deliberately scoped to composing Payment/Refund
/// (money in/out) per its own doc comments; a product-sales report isn't a
/// financial-ledger concern in that same sense, so it gets its own service
/// rather than blurring that boundary.
/// </summary>
public interface IReportingService
{
    /// <summary>
    /// Quantity/revenue sold per product, across successfully-paid orders
    /// placed within the range, highest quantity first. Fills a gap flagged
    /// repeatedly elsewhere in this app (CatalogBrowseModels, Recommendation
    /// service) as "no order/sales history to sort by yet" - that history
    /// now fully exists.
    /// </summary>
    Task<TopSellingProductsResultDto> GetTopSellingProductsAsync(TopSellingProductsQuery query, CancellationToken cancellationToken = default);
}
