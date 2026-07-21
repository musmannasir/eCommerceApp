using ECommerceApp.Application.Common.Interfaces;

namespace ECommerceApp.Web.Tests.TestSupport;

public class FakeCurrentUserService : ICurrentUserService
{
    public string? UserId { get; set; } = "test-user-id";
    public string? Email { get; set; } = "test@example.com";
    public bool IsAuthenticated { get; set; } = true;
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    public string? CorrelationId { get; set; }

    public bool IsInRole(string role) => Roles.Contains(role);
}
