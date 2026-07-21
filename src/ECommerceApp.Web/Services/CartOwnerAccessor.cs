using System.Security.Claims;
using ECommerceApp.Application.Carts.Models;

namespace ECommerceApp.Web.Services;

public sealed class CartOwnerAccessor : ICartOwnerAccessor
{
    private const string CookieName = "CartGuestToken";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;

    public CartOwnerAccessor(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    public CartOwner GetOrCreateOwner()
    {
        var userId = GetAuthenticatedUserId();
        if (userId is not null)
        {
            return CartOwner.ForUser(userId);
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var existingToken = httpContext?.Request.Cookies[CookieName];
        if (!string.IsNullOrEmpty(existingToken))
        {
            return CartOwner.ForGuest(existingToken);
        }

        var newToken = Guid.NewGuid().ToString("N");
        httpContext?.Response.Cookies.Append(CookieName, newToken, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = !_environment.IsDevelopment(),
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            IsEssential = true,
        });

        return CartOwner.ForGuest(newToken);
    }

    public CartOwner? TryGetOwner()
    {
        var userId = GetAuthenticatedUserId();
        if (userId is not null)
        {
            return CartOwner.ForUser(userId);
        }

        var existingToken = _httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
        return string.IsNullOrEmpty(existingToken) ? null : CartOwner.ForGuest(existingToken);
    }

    public string? TryGetGuestToken()
    {
        var token = _httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
        return string.IsNullOrEmpty(token) ? null : token;
    }

    public void ClearGuestToken() => _httpContextAccessor.HttpContext?.Response.Cookies.Delete(CookieName);

    private string? GetAuthenticatedUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    }
}
