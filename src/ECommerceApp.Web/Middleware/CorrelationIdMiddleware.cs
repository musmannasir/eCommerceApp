using Serilog.Context;

namespace ECommerceApp.Web.Middleware;

/// <summary>
/// Ensures every request has a correlation id (reusing an inbound
/// <c>X-Correlation-Id</c> header when the caller supplied one), echoes it back
/// on the response, and pushes it into the Serilog log context so every log
/// line for the request can be tied together.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;
        context.Items[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
