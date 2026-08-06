using ECommerceApp.Application.Security;
using ECommerceApp.Application.Security.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

/// <summary>
/// Milestone 16.2 - a viewer over the security audit log that's existed
/// since Milestone 1. Gated by CanManageUsers (SuperAdmin/Admin) - the log
/// is predominantly about user/account security actions, so it stays at
/// the same tier as the Users screen itself, the same reasoning
/// LedgerController uses to justify CanViewFinancialReports over the
/// broader CanManageOrders.
/// </summary>
[Area("Admin")]
[Authorize(Policy = Policies.CanManageUsers)]
public class AuditLogController : Controller
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        SecurityEventType? eventType, bool? succeeded, string? search, DateTime? from, DateTime? to, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var query = new AuditLogQuery
        {
            Page = page,
            EventType = eventType,
            Succeeded = succeeded,
            Search = search,
            From = from,
            To = to,
        };

        var result = await _auditLogService.GetPagedAsync(query, cancellationToken);

        ViewData["EventType"] = eventType;
        ViewData["Succeeded"] = succeeded;
        ViewData["Search"] = search;
        ViewData["From"] = from ?? to?.AddDays(-30) ?? DateTime.UtcNow.Date.AddDays(-30);
        ViewData["To"] = to ?? DateTime.UtcNow.Date;
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(
        SecurityEventType? eventType, bool? succeeded, string? search, DateTime? from, DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var entries = await _auditLogService.GetAllAsync(
            new AuditLogQuery { EventType = eventType, Succeeded = succeeded, Search = search, From = from, To = to },
            cancellationToken);

        var csv = CsvExport.BuildCsv(
            new[] { "Date", "Event", "Succeeded", "User", "IP address", "Details" },
            entries.Select(e => new[]
            {
                e.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm"),
                e.EventType.ToString(),
                e.Succeeded ? "Yes" : "No",
                e.UserEmail ?? string.Empty,
                e.IpAddress ?? string.Empty,
                e.Details ?? string.Empty,
            }));

        return File(csv, "text/csv", "audit-log.csv");
    }
}
