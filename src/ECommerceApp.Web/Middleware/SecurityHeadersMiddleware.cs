namespace ECommerceApp.Web.Middleware;

/// <summary>
/// Milestone 17.1 - response headers `Security.md` had explicitly flagged as
/// deliberately not built through Milestone 16. `script-src`/`style-src` keep
/// 'unsafe-inline' rather than a nonce - the app has ~25 views using inline
/// &lt;script&gt; blocks, onsubmit/onclick/onchange attributes, and inline
/// style="..." (audited this milestone), and rewriting all of them (including
/// every admin delete-confirmation dialog) is real, behavior-sensitive work
/// better scoped as its own follow-up than folded into a headers middleware.
/// The other directives (object-src, base-uri, form-action, frame-ancestors,
/// default-src) still meaningfully restrict what an injected payload could do.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    private const string PermissionsPolicy =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = PermissionsPolicy;
        headers["Content-Security-Policy"] = ContentSecurityPolicy;

        return _next(context);
    }
}
