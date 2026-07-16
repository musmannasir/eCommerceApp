using ECommerceApp.Domain.Common;

namespace ECommerceApp.Domain.Security;

/// <summary>
/// A rotating JWT refresh token. Only a SHA-256 hash of the token value is ever
/// stored; the raw value is returned to the caller once and never persisted.
/// Rotation chains (<see cref="ReplacedByTokenHash"/>) are what make reuse
/// detection possible: presenting an already-rotated token is a signal the
/// token was stolen, and revokes the whole chain.
/// </summary>
public class RefreshToken : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? ReasonRevoked { get; set; }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;
    public bool IsRevoked => RevokedAtUtc is not null;
    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTime utcNow, string? revokedByIp, string reason, string? replacedByTokenHash = null)
    {
        RevokedAtUtc = utcNow;
        RevokedByIp = revokedByIp;
        ReasonRevoked = reason;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
