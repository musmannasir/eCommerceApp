namespace ECommerceApp.Application.Auth.Models;

public record RegisterRequest(string Email, string Password, string FirstName, string LastName);

public record LoginRequest(string Email, string Password);

public record ForgotPasswordRequest(string Email);

public record CurrentUserDto(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    DateTime CreatedAtUtc,
    DateTime? LastSuccessfulLoginAtUtc);

public record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public record LoginResult(CurrentUserDto User, AuthTokens Tokens);

public record ChangePasswordRequest(string UserId, string CurrentPassword, string NewPassword);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);
