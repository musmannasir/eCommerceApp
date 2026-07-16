using System.Security.Claims;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Web.Middleware;

namespace ECommerceApp.Web.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    public string? CorrelationId =>
        _httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.HeaderName] as string;
}
