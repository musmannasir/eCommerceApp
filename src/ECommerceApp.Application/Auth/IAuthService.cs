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
    /// Always returns a success <see cref="Result{T}"/> to avoid leaking whether the email is
    /// registered; the token value is null when there is no matching active account, and the
    /// caller must show the same generic message either way and only send an email when non-null.
    /// </summary>
    Task<Result<string?>> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task<Result<CurrentUserDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
}
