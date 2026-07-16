using System.Security.Claims;
using ECommerceApp.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace ECommerceApp.Web.Tests.Services;

public class CurrentUserServiceTests
{
    private static IHttpContextAccessor CreateAccessor(ClaimsPrincipal? user)
    {
        var context = new DefaultHttpContext();
        if (user is not null)
        {
            context.User = user;
        }

        return new HttpContextAccessor { HttpContext = context };
    }

    [Fact]
    public void Anonymous_request_reports_not_authenticated_with_no_claims()
    {
        var service = new CurrentUserService(CreateAccessor(new ClaimsPrincipal(new ClaimsIdentity())));

        service.IsAuthenticated.Should().BeFalse();
        service.UserId.Should().BeNull();
        service.Roles.Should().BeEmpty();
    }

    [Fact]
    public void Authenticated_request_exposes_user_id_email_and_roles()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Email, "user@example.com"),
            new Claim(ClaimTypes.Role, "Admin"),
        ], authenticationType: "TestAuth");

        var service = new CurrentUserService(CreateAccessor(new ClaimsPrincipal(identity)));

        service.IsAuthenticated.Should().BeTrue();
        service.UserId.Should().Be("user-1");
        service.Email.Should().Be("user@example.com");
        service.Roles.Should().Contain("Admin");
        service.IsInRole("Admin").Should().BeTrue();
        service.IsInRole("SuperAdmin").Should().BeFalse();
    }

    [Fact]
    public void No_http_context_reports_not_authenticated()
    {
        var service = new CurrentUserService(new HttpContextAccessor());

        service.IsAuthenticated.Should().BeFalse();
        service.UserId.Should().BeNull();
    }
}
