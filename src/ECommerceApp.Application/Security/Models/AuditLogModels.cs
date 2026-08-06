using ECommerceApp.Domain.Security;

namespace ECommerceApp.Application.Security.Models;

public record AuditLogEntryDto(
    int Id,
    string? UserId,
    string? UserEmail,
    SecurityEventType EventType,
    DateTime OccurredAtUtc,
    bool Succeeded,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    string? Details);

/// <summary>
/// <see cref="From"/>/<see cref="To"/> default to the 30 days ending today
/// when not given, the same convention <c>CashFlowQuery</c> (Milestone
/// 14.2) established - security audit rows accumulate far faster than
/// Payment/Refund rows (every login attempt, not just every order), so an
/// unbounded default view isn't appropriate here.
/// </summary>
public record AuditLogQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public SecurityEventType? EventType { get; init; }
    public bool? Succeeded { get; init; }
    public string? Search { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
