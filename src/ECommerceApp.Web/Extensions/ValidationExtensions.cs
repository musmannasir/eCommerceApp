using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ECommerceApp.Web.Extensions;

public static class ValidationExtensions
{
    /// <summary>Runs a FluentValidation validator and returns a 400 ValidationProblem if it fails, or null if it passes.</summary>
    public static async Task<IActionResult?> ValidateOrNullAsync<T>(
        this ControllerBase controller,
        IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
        {
            return null;
        }

        var modelState = new ModelStateDictionary();
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return controller.ValidationProblem(modelState);
    }
}
