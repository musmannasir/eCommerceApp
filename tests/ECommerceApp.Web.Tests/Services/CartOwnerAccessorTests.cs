using System.Security.Claims;
using ECommerceApp.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace ECommerceApp.Web.Tests.Services;

public class CartOwnerAccessorTests
{
    private static (HttpContext Context, CartOwnerAccessor Accessor) CreateGuestAccessor()
    {
        var context = new DefaultHttpContext();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Development");
        var accessor = new CartOwnerAccessor(new HttpContextAccessor { HttpContext = context }, environment.Object);
        return (context, accessor);
    }

    private static CartOwnerAccessor CreateAuthenticatedAccessor(string userId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "TestAuth"));
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Development");
        return new CartOwnerAccessor(new HttpContextAccessor { HttpContext = context }, environment.Object);
    }

    [Fact]
    public void An_authenticated_request_resolves_to_a_user_owner()
    {
        var accessor = CreateAuthenticatedAccessor("user-1");

        var owner = accessor.GetOrCreateOwner();

        owner.UserId.Should().Be("user-1");
        owner.GuestToken.Should().BeNull();
    }

    [Fact]
    public void A_guest_with_no_cookie_gets_a_new_token_and_a_response_cookie_is_set()
    {
        var (context, accessor) = CreateGuestAccessor();

        var owner = accessor.GetOrCreateOwner();

        owner.UserId.Should().BeNull();
        owner.GuestToken.Should().NotBeNullOrEmpty();
        context.Response.Headers.SetCookie.Should().Contain(h => h!.Contains("CartGuestToken"));
    }

    [Fact]
    public void A_guest_with_an_existing_cookie_reuses_it_without_setting_a_new_one()
    {
        var (context, accessor) = CreateGuestAccessor();
        context.Request.Headers.Append("Cookie", "CartGuestToken=existing-token-value");

        var owner = accessor.GetOrCreateOwner();

        owner.GuestToken.Should().Be("existing-token-value");
        context.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public void TryGetOwner_returns_null_for_a_guest_with_no_cookie_and_sets_no_cookie()
    {
        var (context, accessor) = CreateGuestAccessor();

        var owner = accessor.TryGetOwner();

        owner.Should().BeNull();
        context.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public void TryGetOwner_returns_the_authenticated_user_without_touching_cookies()
    {
        var accessor = CreateAuthenticatedAccessor("user-1");

        var owner = accessor.TryGetOwner();

        owner.Should().NotBeNull();
        owner!.UserId.Should().Be("user-1");
    }

    [Fact]
    public void TryGetGuestToken_reads_the_cookie_directly_regardless_of_auth_state()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Append("Cookie", "CartGuestToken=guest-token-value");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")], authenticationType: "TestAuth"));
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Development");
        var accessor = new CartOwnerAccessor(new HttpContextAccessor { HttpContext = context }, environment.Object);

        accessor.TryGetGuestToken().Should().Be("guest-token-value");
    }

    [Fact]
    public void TryGetGuestToken_returns_null_when_there_is_no_cookie()
    {
        var (_, accessor) = CreateGuestAccessor();

        accessor.TryGetGuestToken().Should().BeNull();
    }

    [Fact]
    public void ClearGuestToken_issues_a_delete_for_the_cookie()
    {
        var (context, accessor) = CreateGuestAccessor();

        accessor.ClearGuestToken();

        context.Response.Headers.SetCookie.Should().Contain(h => h!.Contains("CartGuestToken") && h.Contains("expires="));
    }
}
