using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerceApp.Application.Auth;
using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Options;
using ECommerceApp.Application.Notifications.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Notifications;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Identity;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ECommerceApp.Infrastructure.Security;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUserService;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext,
        IClock clock,
        ICurrentUserService currentUserService,
        IOptions<JwtSettings> jwtOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
        _clock = clock;
        _currentUserService = currentUserService;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<Result<CurrentUserDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            AddAuditEvent(null, SecurityEventType.RegisterFailure, succeeded: false, details: "Duplicate email.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<CurrentUserDto>(Error.Conflict("auth.duplicate_email", "An account with this email already exists."));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow,
            PasswordChangedAtUtc = _clock.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            AddAuditEvent(null, SecurityEventType.RegisterFailure, succeeded: false, details: errors);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<CurrentUserDto>(Error.Validation("auth.register_failed", errors));
        }

        await _userManager.AddToRoleAsync(user, Roles.Customer);

        AddAuditEvent(user.Id, SecurityEventType.RegisterSuccess, succeeded: true);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CurrentUserDto(
            user.Id, user.Email!, user.FirstName, user.LastName, [Roles.Customer], user.CreatedAtUtc, user.LastSuccessfulLoginAtUtc));
    }

    public async Task<Result<CurrentUserDto>> ValidateCredentialsAsync(
        LoginRequest request,
        LoginMethod loginMethod,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var invalidCredentials = Result.Failure<CurrentUserDto>(
            Error.Unauthorized("auth.invalid_credentials", "Invalid email or password."));

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            AddAuditEvent(null, SecurityEventType.LoginFailure, succeeded: false, ipAddress, userAgent, "Unknown email.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return invalidCredentials;
        }

        if (!user.IsActive)
        {
            AddAuditEvent(user.Id, SecurityEventType.LoginFailure, succeeded: false, ipAddress, userAgent, "Account disabled.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return invalidCredentials;
        }

        var checkResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (checkResult.IsLockedOut)
        {
            AddAuditEvent(user.Id, SecurityEventType.AccountLockedOut, succeeded: false, ipAddress, userAgent);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<CurrentUserDto>(Error.Forbidden(
                "auth.locked_out",
                "This account is temporarily locked due to multiple failed login attempts. Please try again later."));
        }

        if (!checkResult.Succeeded)
        {
            AddAuditEvent(user.Id, SecurityEventType.LoginFailure, succeeded: false, ipAddress, userAgent, "Invalid password.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return invalidCredentials;
        }

        user.LastSuccessfulLoginAtUtc = _clock.UtcNow;
        await _userManager.UpdateAsync(user);

        _dbContext.UserSessions.Add(new UserSession
        {
            UserId = user.Id,
            LoginMethod = loginMethod,
            LoginAtUtc = _clock.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        });

        AddAuditEvent(user.Id, SecurityEventType.LoginSuccess, succeeded: true, ipAddress, userAgent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        return Result.Success(new CurrentUserDto(
            user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList(), user.CreatedAtUtc, user.LastSuccessfulLoginAtUtc));
    }

    public async Task<Result<AuthTokens>> IssueTokensAsync(string userId, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure<AuthTokens>(Error.NotFound("auth.user_not_found", "User not found."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, accessExpiresAtUtc) = GenerateAccessToken(user, roles);
        var (rawRefreshToken, refreshEntity) = CreateRefreshToken(user.Id, ipAddress);

        _dbContext.RefreshTokens.Add(refreshEntity);
        AddAuditEvent(user.Id, SecurityEventType.RefreshTokenIssued, succeeded: true, ipAddress, userAgent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokens(accessToken, accessExpiresAtUtc, rawRefreshToken, refreshEntity.ExpiresAtUtc));
    }

    public async Task<Result<AuthTokens>> RefreshTokenAsync(string rawRefreshToken, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(rawRefreshToken);
        var token = await _dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        var invalidToken = Result.Failure<AuthTokens>(
            Error.Unauthorized("auth.invalid_refresh_token", "This session is no longer valid. Please log in again."));

        if (token is null)
        {
            AddAuditEvent(null, SecurityEventType.RefreshTokenReuseDetected, succeeded: false, ipAddress, userAgent, "Unknown refresh token presented.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return invalidToken;
        }

        if (token.IsRevoked)
        {
            var activeTokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == token.UserId && t.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var active in activeTokens)
            {
                active.Revoke(_clock.UtcNow, ipAddress, "reuse_detected");
            }

            AddAuditEvent(token.UserId, SecurityEventType.RefreshTokenReuseDetected, succeeded: false, ipAddress, userAgent);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return invalidToken;
        }

        if (token.IsExpired(_clock.UtcNow))
        {
            return invalidToken;
        }

        var user = await _userManager.FindByIdAsync(token.UserId);
        if (user is null || !user.IsActive)
        {
            return invalidToken;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, accessExpiresAtUtc) = GenerateAccessToken(user, roles);
        var (newRawToken, newEntity) = CreateRefreshToken(user.Id, ipAddress);

        token.Revoke(_clock.UtcNow, ipAddress, "rotated", newEntity.TokenHash);
        _dbContext.RefreshTokens.Add(newEntity);

        AddAuditEvent(user.Id, SecurityEventType.RefreshTokenRotated, succeeded: true, ipAddress, userAgent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokens(accessToken, accessExpiresAtUtc, newRawToken, newEntity.ExpiresAtUtc));
    }

    public async Task<Result> LogoutAsync(string? userId, string? rawRefreshToken, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            var hash = HashToken(rawRefreshToken);
            var token = await _dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
            if (token is not null && !token.IsRevoked)
            {
                token.Revoke(_clock.UtcNow, null, "logout");
            }
        }

        AddAuditEvent(userId, SecurityEventType.Logout, succeeded: true);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RevokeAllSessionsAsync(string userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(_clock.UtcNow, ipAddress, "revoke_all_sessions");
        }

        var openSessions = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in openSessions)
        {
            session.IsRevoked = true;
            session.LoggedOutAtUtc = _clock.UtcNow;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is not null)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }

        AddAuditEvent(userId, SecurityEventType.LogoutAllSessions, succeeded: true, ipAddress);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("auth.user_not_found", "User not found."));
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            AddAuditEvent(user.Id, SecurityEventType.PasswordChanged, succeeded: false, details: errors);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure(Error.Validation("auth.change_password_failed", errors));
        }

        user.PasswordChangedAtUtc = _clock.UtcNow;
        await _userManager.UpdateAsync(user);

        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.Revoke(_clock.UtcNow, null, "password_changed");
        }

        AddAuditEvent(user.Id, SecurityEventType.PasswordChanged, succeeded: true);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(string email, Func<string, string> buildResetLink, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is not null && user.IsActive)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var payload = new PasswordResetEmailOutboxPayload(email, buildResetLink(token));

            // Enqueued on the same DbContext, committed by the same SaveChangesAsync
            // call below as the audit event - the outbox row and "a reset was
            // requested" are atomic (Milestone 15.2).
            _dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxMessageType.PasswordResetEmail,
                PayloadJson = JsonSerializer.Serialize(payload),
                CreatedAtUtc = _clock.UtcNow,
            });
        }

        AddAuditEvent(user?.Id, SecurityEventType.PasswordResetRequested, succeeded: user is not null);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var genericFailure = Result.Failure(
            Error.Validation("auth.reset_failed", "Unable to reset the password. The link may be invalid or expired."));

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return genericFailure;
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            AddAuditEvent(user.Id, SecurityEventType.PasswordResetCompleted, succeeded: false, details: errors);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure(Error.Validation("auth.reset_failed", errors));
        }

        user.PasswordChangedAtUtc = _clock.UtcNow;
        await _userManager.UpdateAsync(user);

        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.Revoke(_clock.UtcNow, null, "password_reset");
        }

        AddAuditEvent(user.Id, SecurityEventType.PasswordResetCompleted, succeeded: true);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure<CurrentUserDto>(Error.NotFound("auth.user_not_found", "User not found."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Result.Success(new CurrentUserDto(
            user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList(), user.CreatedAtUtc, user.LastSuccessfulLoginAtUtc));
    }

    private void AddAuditEvent(
        string? userId,
        SecurityEventType eventType,
        bool succeeded,
        string? ipAddress = null,
        string? userAgent = null,
        string? details = null)
    {
        _dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            UserId = userId,
            EventType = eventType,
            Succeeded = succeeded,
            OccurredAtUtc = _clock.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CorrelationId = _currentUserService.CorrelationId,
            Details = details,
        });
    }

    private (string RawToken, RefreshToken Entity) CreateRefreshToken(string userId, string? ipAddress)
    {
        var raw = GenerateRawRefreshToken();
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(raw),
            CreatedAtUtc = _clock.UtcNow,
            CreatedByIp = ipAddress,
            ExpiresAtUtc = _clock.UtcNow.AddDays(_jwtSettings.RefreshTokenDays),
        };

        return (raw, entity);
    }

    private (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        if (string.IsNullOrWhiteSpace(_jwtSettings.Key))
        {
            throw new InvalidOperationException(
                "Jwt:Key is not configured. Set it via User Secrets (see README.md) before issuing tokens.");
        }

        var expiresAtUtc = _clock.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: _clock.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    private static string GenerateRawRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
