using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Security;

public enum LoginMethod
{
    CookieMvc,
    JwtApi,
}

/// <summary>
/// A lightweight audit trail of each successful login, for admin visibility
/// and "revoke all sessions" reporting. It is not consulted to authorize
/// requests - cookie sessions are invalidated via the Identity security
/// stamp, and API sessions via refresh-token revocation - so it never needs
/// to be checked on the request hot path.
/// </summary>
public class UserSession : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public LoginMethod LoginMethod { get; set; }
    public DateTime LoginAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? LoggedOutAtUtc { get; set; }
    public bool IsRevoked { get; set; }
}
