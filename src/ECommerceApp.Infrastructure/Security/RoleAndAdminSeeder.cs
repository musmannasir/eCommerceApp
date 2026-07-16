using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECommerceApp.Infrastructure.Security;

/// <summary>
/// Seeds the fixed role set and the first SuperAdmin from
/// <c>SeedAdmin:Email</c> / <c>SeedAdmin:Password</c> (User Secrets in dev).
/// There is no hardcoded fallback - if either value is missing, admin seeding
/// is skipped and a warning is logged.
/// </summary>
public sealed class RoleAndAdminSeeder
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ILogger<RoleAndAdminSeeder> _logger;

    public RoleAndAdminSeeder(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IClock clock,
        ILogger<RoleAndAdminSeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
        _clock = clock;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in Roles.All)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        var email = _configuration["SeedAdmin:Email"];
        var password = _configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "SeedAdmin:Email / SeedAdmin:Password are not configured - skipping SuperAdmin seeding. " +
                "Configure them via User Secrets before you need to log in as an administrator.");
            return;
        }

        var existingAdmin = await _userManager.FindByEmailAsync(email);
        if (existingAdmin is not null)
        {
            if (!await _userManager.IsInRoleAsync(existingAdmin, Roles.SuperAdmin))
            {
                await _userManager.AddToRoleAsync(existingAdmin, Roles.SuperAdmin);
            }

            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Super",
            LastName = "Admin",
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow,
            PasswordChangedAtUtc = _clock.UtcNow,
        };

        var result = await _userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            _logger.LogError(
                "Failed to seed the SuperAdmin account: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await _userManager.AddToRoleAsync(admin, Roles.SuperAdmin);
    }
}
