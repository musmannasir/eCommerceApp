using ECommerceApp.Application.Finance;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Models.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

/// <summary>
/// The admin area's landing page - stays open to every staff role (unlike
/// LedgerController) so it keeps working as the front door to the admin
/// area; only the financial summary cards it now shows (Milestone 14.1) are
/// gated behind CanViewFinancialReports, checked here rather than in the
/// view so an unauthorized role never even triggers the aggregate queries.
/// </summary>
[Area("Admin")]
[Authorize(Roles = Roles.StaffRolesCsv)]
public class HomeController : Controller
{
    private readonly IFinanceService _financeService;
    private readonly IAuthorizationService _authorizationService;

    public HomeController(IFinanceService financeService, IAuthorizationService authorizationService)
    {
        _financeService = financeService;
        _authorizationService = authorizationService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var canViewFinancials = (await _authorizationService.AuthorizeAsync(User, Policies.CanViewFinancialReports)).Succeeded;
        var summary = canViewFinancials ? await _financeService.GetDashboardSummaryAsync(cancellationToken) : null;

        return View(new AdminDashboardViewModel(summary));
    }
}
