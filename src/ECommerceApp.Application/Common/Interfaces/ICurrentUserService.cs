namespace ECommerceApp.Application.Common.Interfaces;

/// <summary>
/// Exposes the identity of the caller for the duration of the current request
/// (or background-job scope), so application services and the persistence
/// layer can stamp audit fields without depending on ASP.NET Core directly.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }
    string? CorrelationId { get; }
    bool IsInRole(string role);
}
