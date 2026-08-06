using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Application.Notifications.Models;
using ECommerceApp.Domain.Notifications;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Tests.Security;

public class PasswordManagementTests : IAsyncLifetime
{
    private AuthServiceTestHarness _harness = null!;
    private const string Email = "password.user@example.com";

    public async Task InitializeAsync()
    {
        _harness = await AuthServiceTestHarness.CreateAsync();
        await _harness.AuthService.RegisterAsync(
            new RegisterRequest(Email, AuthServiceTestHarness.ValidPassword, "Password", "User"));
    }

    public Task DisposeAsync()
    {
        _harness.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ChangePassword_with_the_correct_current_password_succeeds()
    {
        var user = await _harness.UserManager.FindByEmailAsync(Email);

        var result = await _harness.AuthService.ChangePasswordAsync(
            new ChangePasswordRequest(user!.Id, AuthServiceTestHarness.ValidPassword, "NewStr0ng!Passw0rd"));

        result.IsSuccess.Should().BeTrue();

        var loginWithNewPassword = await _harness.AuthService.ValidateCredentialsAsync(
            new LoginRequest(Email, "NewStr0ng!Passw0rd"), Domain.Security.LoginMethod.CookieMvc, "127.0.0.1", "agent");
        loginWithNewPassword.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ForgotPassword_then_ResetPassword_with_the_issued_token_succeeds()
    {
        // The token never leaves ForgotPasswordAsync directly (Milestone
        // 15.2) - it travels inside the enqueued outbox row's ResetLink, so
        // the identity buildResetLink below lets the test recover the raw
        // token from there instead.
        var forgotResult = await _harness.AuthService.ForgotPasswordAsync(Email, token => token);

        forgotResult.IsSuccess.Should().BeTrue();

        var token = await GetEnqueuedResetTokenAsync();
        token.Should().NotBeNullOrEmpty();

        var resetResult = await _harness.AuthService.ResetPasswordAsync(
            new ResetPasswordRequest(Email, token!, "ResetStr0ng!Passw0rd"));

        resetResult.IsSuccess.Should().BeTrue();

        var loginWithNewPassword = await _harness.AuthService.ValidateCredentialsAsync(
            new LoginRequest(Email, "ResetStr0ng!Passw0rd"), Domain.Security.LoginMethod.CookieMvc, "127.0.0.1", "agent");
        loginWithNewPassword.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ForgotPassword_for_an_unknown_email_still_returns_success_but_enqueues_nothing()
    {
        var result = await _harness.AuthService.ForgotPasswordAsync("nobody@example.com", token => token);

        result.IsSuccess.Should().BeTrue();
        _harness.DbContext.OutboxMessages.Should().BeEmpty();
    }

    private async Task<string?> GetEnqueuedResetTokenAsync()
    {
        var message = await _harness.DbContext.OutboxMessages.SingleAsync(m => m.Type == OutboxMessageType.PasswordResetEmail);
        var payload = System.Text.Json.JsonSerializer.Deserialize<PasswordResetEmailOutboxPayload>(message.PayloadJson);
        return payload?.ResetLink;
    }
}
