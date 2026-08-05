using ECommerceApp.Application.Finance.Models;

namespace ECommerceApp.Web.Models.Home;

/// <summary>
/// FinancialSummary is null for a staff role that fails the
/// CanViewFinancialReports policy check (Milestone 14.1) - the dashboard
/// itself stays open to every staff role so it keeps working as the admin
/// area's landing page, but the money figures are computed and shown only
/// for SuperAdmin/Admin.
/// </summary>
public record AdminDashboardViewModel(FinancialSummaryDto? FinancialSummary);
