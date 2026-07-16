using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Application-layer services: FluentValidation validators discovered
    /// in this assembly, and (as milestones add them) application services.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }
}
