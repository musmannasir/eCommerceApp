using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Tests.Security;

public class SessionManagementTests : IAsyncLifetime
{
    private AuthServiceTestHarness _harness = null!;
    private string _userId = null!;

    public async Task InitializeAsync()
    {
        _harness = await AuthServiceTestHarness.CreateAsync();
        var register = await _harness.AuthService.RegisterAsync(
            new RegisterRequest("sessions.user@example.com", AuthServiceTestHarness.ValidPassword, "Sessions", "User"));
        _userId = register.Value.UserId;
    }

    public Task DisposeAsync()
    {
        _harness.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Logout_revokes_the_presented_refresh_token()
    {
        var issued = await _harness.AuthService.IssueTokensAsync(_userId, "127.0.0.1", "agent");

        var result = await _harness.AuthService.LogoutAsync(_userId, issued.Value.RefreshToken);

        result.IsSuccess.Should().BeTrue();

        var refreshAfterLogout = await _harness.AuthService.RefreshTokenAsync(issued.Value.RefreshToken, "127.0.0.1", "agent");
        refreshAfterLogout.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Revoking_all_sessions_invalidates_every_active_refresh_token_and_bumps_the_security_stamp()
    {
        var deviceOne = await _harness.AuthService.IssueTokensAsync(_userId, "127.0.0.1", "device-one");
        var deviceTwo = await _harness.AuthService.IssueTokensAsync(_userId, "127.0.0.2", "device-two");

        var userBefore = await _harness.UserManager.FindByIdAsync(_userId);
        var stampBefore = userBefore!.SecurityStamp;

        var result = await _harness.AuthService.RevokeAllSessionsAsync(_userId, "127.0.0.1");

        result.IsSuccess.Should().BeTrue();

        var activeTokens = await _harness.DbContext.RefreshTokens
            .Where(t => t.UserId == _userId && t.RevokedAtUtc == null)
            .ToListAsync();
        activeTokens.Should().BeEmpty();

        var userAfter = await _harness.UserManager.FindByIdAsync(_userId);
        userAfter!.SecurityStamp.Should().NotBe(stampBefore);

        (await _harness.AuthService.RefreshTokenAsync(deviceOne.Value.RefreshToken, "1.1.1.1", "a")).IsFailure.Should().BeTrue();
        (await _harness.AuthService.RefreshTokenAsync(deviceTwo.Value.RefreshToken, "1.1.1.1", "a")).IsFailure.Should().BeTrue();
    }
}
