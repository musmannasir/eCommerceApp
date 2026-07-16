using ECommerceApp.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Extensions;

public static class ResultExtensions
{
    /// <summary>Maps a failed <see cref="Result"/>'s <see cref="Error"/> to the matching ProblemDetails HTTP response.</summary>
    public static IActionResult ToProblem(this ControllerBase controller, Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return controller.Problem(detail: error.Message, statusCode: status, title: error.Code);
    }
}
