namespace ECommerceApp.Domain.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Unexpected,
}

/// <summary>
/// A machine-readable, framework-agnostic application error. Web/API layers
/// translate this into the appropriate HTTP status / ProblemDetails response.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);

    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Unexpected);
}
