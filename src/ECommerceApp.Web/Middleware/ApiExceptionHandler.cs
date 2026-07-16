using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Middleware;

/// <summary>
/// Converts unhandled exceptions on <c>/api/*</c> requests into RFC 7807
/// <see cref="ProblemDetails"/> responses instead of the MVC error view.
/// Requests outside <c>/api</c> are left unhandled here so the pipeline falls
/// back to the configured MVC error page (<c>/Home/Error</c>), which never
/// exposes exception details to the user.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
        {
            return false;
        }

        _logger.LogError(
            exception,
            "Unhandled exception processing {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "The request could not be completed. Please try again later.",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Instance = httpContext.Request.Path,
        };

        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
