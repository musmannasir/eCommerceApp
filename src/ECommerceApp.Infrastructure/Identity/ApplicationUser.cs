using Microsoft.AspNetCore.Identity;

namespace ECommerceApp.Infrastructure.Identity;

/// <summary>
/// The Identity user record. Lives in Infrastructure (not Domain) because it
/// must extend the framework's <see cref="IdentityUser"/>; Domain stays free
/// of any framework dependency. Failed-login tracking and temporary lockout
/// are already provided by the base class's AccessFailedCount/LockoutEnd;
/// <see cref="IsActive"/> is a separate, admin-controlled permanent disable switch.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastSuccessfulLoginAtUtc { get; set; }
    public DateTime? PasswordChangedAtUtc { get; set; }
}
