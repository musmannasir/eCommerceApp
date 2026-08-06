namespace ECommerceApp.Application.Users.Models;

public record UserListItemDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastSuccessfulLoginAtUtc);

public record UserDetailDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastSuccessfulLoginAtUtc,
    DateTime? PasswordChangedAtUtc,
    bool IsLockedOut);

public record UserQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? Role { get; init; }
    public bool? ActiveOnly { get; init; }
}

public record CreateUserRequest(string Email, string Password, string FirstName, string LastName, string Role);

public record UpdateUserRequest(string FirstName, string LastName, string Role);
