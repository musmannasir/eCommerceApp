using ECommerceApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.TestSupport;

public static class UserProvisioningExtensions
{
    /// <summary>
    /// Creates a user directly via UserManager and assigns a role that isn't self-assignable
    /// through public registration (e.g. CatalogManager) - registration always assigns Customer.
    /// </summary>
    public static async Task CreateUserInRoleAsync(this WebApplicationFactory<Web.Program> factory, string email, string password, string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = role,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, role);
    }
}
