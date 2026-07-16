using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Security;

public class RegistrationTests : IAsyncLifetime
{
    private AuthServiceTestHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await AuthServiceTestHarness.CreateAsync();
    public Task DisposeAsync()
    {
        _harness.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Registering_a_new_user_succeeds_and_assigns_the_Customer_role()
    {
        var request = new RegisterRequest("new.customer@example.com", AuthServiceTestHarness.ValidPassword, "New", "Customer");

        var result = await _harness.AuthService.RegisterAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("new.customer@example.com");
        result.Value.Roles.Should().Contain(Roles.Customer);

        var user = await _harness.UserManager.FindByEmailAsync(request.Email);
        user.Should().NotBeNull();
        user!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Registering_with_an_email_already_in_use_is_rejected()
    {
        var request = new RegisterRequest("duplicate@example.com", AuthServiceTestHarness.ValidPassword, "First", "User");
        await _harness.AuthService.RegisterAsync(request);

        var secondAttempt = await _harness.AuthService.RegisterAsync(
            request with { FirstName = "Second", LastName = "User" });

        secondAttempt.IsFailure.Should().BeTrue();
        secondAttempt.FirstError.Type.Should().Be(Domain.Common.ErrorType.Conflict);
    }

    [Fact]
    public async Task Registering_with_a_weak_password_is_rejected_by_the_password_policy()
    {
        var request = new RegisterRequest("weakpassword@example.com", "abc", "Weak", "Password");

        var result = await _harness.AuthService.RegisterAsync(request);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(Domain.Common.ErrorType.Validation);

        var user = await _harness.UserManager.FindByEmailAsync(request.Email);
        user.Should().BeNull();
    }
}
