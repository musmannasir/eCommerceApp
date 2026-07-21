using ECommerceApp.Application.Carts.Models;

namespace ECommerceApp.Web.Services;

/// <summary>
/// Resolves which cart the current request belongs to. Lives in the Web
/// project, not Infrastructure/Application, for the same HttpContext-dependent
/// reasoning as ICurrentUserService/RecentlyViewedService - CartService itself
/// takes a plain CartOwner and has no knowledge of cookies or claims.
/// </summary>
public interface ICartOwnerAccessor
{
    /// <summary>Resolves the current owner, creating and setting a new guest cookie if this is a guest's first cart-writing request.</summary>
    CartOwner GetOrCreateOwner();

    /// <summary>Resolves the current owner without creating a guest cookie - used for read-only requests so a visitor who never adds anything never gets one.</summary>
    CartOwner? TryGetOwner();

    /// <summary>
    /// Reads the guest cart cookie directly, bypassing TryGetOwner()'s auth-state
    /// branching. Needed by the login/register flow: ASP.NET Core's cookie
    /// sign-in doesn't update HttpContext.User until the *next* request, so
    /// immediately after a successful sign-in, TryGetOwner() would still see
    /// an anonymous user - this reads the cookie unconditionally instead.
    /// </summary>
    string? TryGetGuestToken();

    /// <summary>Deletes the guest cart cookie - called once a guest cart has been merged into a signed-in user's cart, since the token no longer refers to anything.</summary>
    void ClearGuestToken();
}
