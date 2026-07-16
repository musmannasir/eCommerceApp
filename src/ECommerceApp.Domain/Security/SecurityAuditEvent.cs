using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Security;

/// <summary>
/// An immutable security audit log entry. Never soft-deleted or edited -
/// corrections happen by writing a new event, never by mutating history.
/// <see cref="Details"/> must never contain a password, token, or other
/// secret value.
/// </summary>
public class SecurityAuditEvent : BaseEntity
{
    public string? UserId { get; set; }
    public SecurityEventType EventType { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? Details { get; set; }
}
