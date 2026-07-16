using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Security;

public class LoginTests : IAsyncLifetime
{
    private AuthServiceTestHarness _harness = null!;
    private const string Email = "login.user@example.com";

    public async Task InitializeAsync()
    {
        _harness = await AuthServiceTestHarness.CreateAsync();
        await _harness.AuthService.RegisterAsync(new RegisterRequest(Email, AuthServiceTestHarness.ValidPassword, "Login", "User"));
    }

    public Task DisposeAsync()
    {
        _harness.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Valid_credentials_succeed_and_record_the_login()
    {
        var result = await _harness.AuthService.ValidateCredentialsAsync(
            new LoginRequest(Email, AuthServiceTestHarness.ValidPassword), LoginMethod.CookieMvc, "127.0.0.1", "test-agent");

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be(Email);

        var user = await _harness.UserManager.FindByEmailAsync(Email);
        user!.LastSuccessfulLoginAtUtc.Should().Be(_harness.Clock.UtcNow);
    }

    [Fact]
    public async Task Invalid_password_returns_a_generic_unauthorized_error()
    {
        var result = await _harness.AuthService.ValidateCredentialsAsync(
            new LoginRequest(Email, "TotallyWrongPassword1!"), LoginMethod.CookieMvc, "127.0.0.1", "test-agent");

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Unauthorized);
        result.FirstError.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Unknown_email_returns_the_same_generic_error_as_a_wrong_password()
    {
        var result = await _harness.AuthService.ValidateCredentialsAsync(
            new LoginRequest("nobody@example.com", "whatever-Password1!"), LoginMethod.CookieMvc, "127.0.0.1", "test-agent");

        result.IsFailure.Should().BeTrue();
        result.FirstError.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Account_locks_out_after_the_configured_number_of_failed_attempts()
    {
        for (var i = 0; i < AuthServiceTestHarness.TestMaxFailedAccessAttempts - 1; i++)
        {
            var attempt = await _harness.AuthService.ValidateCredentialsAsync(
                new LoginRequest(Email, "WrongPassword1!"), LoginMethod.CookieMvc, "127.0.0.1", "test-agent");
            attempt.FirstError.Type.Should().Be(ErrorType.Unauthorized);
        }

        var lockingAttempt = await _harness.AuthService.ValidateCredentialsAsync(
            new LoginRequest(Email, "WrongPassword1!"), LoginMethod.CookieMvc, "127.0.0.1", "test-agent");
        lockingAttempt.FirstError.Type.Should().Be(ErrorType.Forbidden);

        var evenWithCorrectPassword = await _harness.AuthService.ValidateCredentialsAsync(
            new LoginRequest(Email, AuthServiceTestHarness.ValidPassword), LoginMethod.CookieMvc, "127.0.0.1", "test-agent");
        evenWithCorrectPassword.IsFailure.Should().BeTrue();
        evenWithCorrectPassword.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }
}
