using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Security;

namespace ECommerceApp.Application.Auth;

/// <summary>
/// Owns the shared auth business rules (lockout, audit logging, token
/// lifecycle) used by both the cookie-based MVC site and the JWT API, so
/// neither surface duplicates them. Implemented in Infrastructure, since it
/// needs <c>UserManager</c>/<c>SignInManager</c>.
/// </summary>
public interface IAuthService
{
    Task<Result<CurrentUserDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>Checks credentials, enforces lockout, and writes the audit trail. Does not itself sign anyone in.</summary>
    Task<Result<CurrentUserDto>> ValidateCredentialsAsync(
        LoginRequest request,
        LoginMethod loginMethod,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result<AuthTokens>> IssueTokensAsync(string userId, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task<Result<AuthTokens>> RefreshTokenAsync(string rawRefreshToken, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(string? userId, string? rawRefreshToken, CancellationToken cancellationToken = default);

    Task<Result> RevokeAllSessionsAsync(string userId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Always returns success to avoid leaking whether the email is registered - the caller must
    /// show the same generic message either way. When a matching active account exists, generates
    /// a reset token, builds the reset link via <paramref name="buildResetLink"/> (kept a caller
    /// callback since only the Web layer can build a URL), and enqueues a password-reset email on
    /// the transactional outbox (Milestone 15.2) atomically with this call's own audit event -
    /// there is no token for the caller to receive directly, since it never leaves this method.
    /// </summary>
    Task<Result> ForgotPasswordAsync(string email, Func<string, string> buildResetLink, CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task<Result<CurrentUserDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
}
