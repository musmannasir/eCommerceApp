using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Security;
using ECommerceApp.Application.Security.Models;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Security;

/// <summary>
/// Applies EventType/Succeeded/date-range filters server-side (simple,
/// provider-agnostic predicates), then resolves each row's user email via
/// one non-correlated lookup rather than a per-row join - the same
/// "materialize then process" approach <c>FinanceService</c> (Milestone
/// 14.1/14.2) established, so results are identical across the EF Core
/// InMemory (unit tests) and SQL Server (real) providers. Search-by-email
/// and pagination both happen after that resolution, since the email isn't
/// a column on <see cref="Domain.Security.SecurityAuditEvent"/> itself.
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;

    public AuditLogService(ApplicationDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<PagedResult<AuditLogEntryDto>> GetPagedAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var matching = await BuildMatchingEntriesAsync(query, cancellationToken);

        var page = matching
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<AuditLogEntryDto>(page, matching.Count, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<AuditLogEntryDto>> GetAllAsync(AuditLogQuery query, CancellationToken cancellationToken = default) =>
        await BuildMatchingEntriesAsync(query, cancellationToken);

    private async Task<List<AuditLogEntryDto>> BuildMatchingEntriesAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        var to = (query.To ?? _clock.UtcNow).Date.AddDays(1);
        var from = (query.From ?? to.AddDays(-30)).Date;
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var eventsQuery = _dbContext.SecurityAuditEvents.Where(e => e.OccurredAtUtc >= from && e.OccurredAtUtc < to);

        if (query.EventType.HasValue)
        {
            eventsQuery = eventsQuery.Where(e => e.EventType == query.EventType.Value);
        }

        if (query.Succeeded.HasValue)
        {
            eventsQuery = eventsQuery.Where(e => e.Succeeded == query.Succeeded.Value);
        }

        // AsNoTracking - this is a pure viewer (see class doc comment: "no new
        // audit-writing capability"), and GetAllAsync's unpaginated CSV-export
        // path can load a genuinely large row count, so skipping EF's change-
        // tracking snapshot for rows that are never saved back is a real saving,
        // not a no-op.
        var events = await eventsQuery.AsNoTracking().OrderByDescending(e => e.Id).ToListAsync(cancellationToken);

        var userIds = events.Where(e => e.UserId is not null).Select(e => e.UserId!).Distinct().ToList();
        var emailByUserId = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var mapped = events.Select(e => new AuditLogEntryDto(
            e.Id, e.UserId, e.UserId is not null ? emailByUserId.GetValueOrDefault(e.UserId) : null,
            e.EventType, e.OccurredAtUtc, e.Succeeded, e.IpAddress, e.UserAgent, e.CorrelationId, e.Details));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            mapped = mapped.Where(e => e.UserEmail is not null && e.UserEmail.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
        }

        return mapped.ToList();
    }
}
