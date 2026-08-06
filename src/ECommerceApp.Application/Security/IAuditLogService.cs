using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Security.Models;

namespace ECommerceApp.Application.Security;

/// <summary>
/// Milestone 16.2 - the read side of the security audit log that's existed
/// since Milestone 1 (<see cref="Domain.Security.SecurityAuditEvent"/>,
/// written by <c>AuthService</c> and, since Milestone 16.1, <c>UserManagementService</c>).
/// Deliberately does not add any new audit-writing capability - this is
/// purely a viewer over what already gets written.
/// </summary>
public interface IAuditLogService
{
    Task<PagedResult<AuditLogEntryDto>> GetPagedAsync(AuditLogQuery query, CancellationToken cancellationToken = default);

    /// <summary>The full feed matching <paramref name="query"/>'s filters, unpaginated - for CSV export.</summary>
    Task<IReadOnlyList<AuditLogEntryDto>> GetAllAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}
