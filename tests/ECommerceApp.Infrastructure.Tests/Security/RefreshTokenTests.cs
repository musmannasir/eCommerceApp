using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Tests.Security;

public class RefreshTokenTests : IAsyncLifetime
{
    private AuthServiceTestHarness _harness = null!;
    private string _userId = null!;

    public async Task InitializeAsync()
    {
        _harness = await AuthServiceTestHarness.CreateAsync();
        var register = await _harness.AuthService.RegisterAsync(
            new RegisterRequest("refresh.user@example.com", AuthServiceTestHarness.ValidPassword, "Refresh", "User"));
        _userId = register.Value.UserId;
    }

    public Task DisposeAsync()
    {
        _harness.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Refreshing_rotates_to_a_new_token_and_revokes_the_old_one()
    {
        var initial = await _harness.AuthService.IssueTokensAsync(_userId, "127.0.0.1", "agent");

        var rotated = await _harness.AuthService.RefreshTokenAsync(initial.Value.RefreshToken, "127.0.0.1", "agent");

        rotated.IsSuccess.Should().BeTrue();
        rotated.Value.RefreshToken.Should().NotBe(initial.Value.RefreshToken);

        var oldTokenHash = Sha256(initial.Value.RefreshToken);
        var oldEntity = await _harness.DbContext.RefreshTokens.SingleAsync(t => t.TokenHash == oldTokenHash);
        oldEntity.IsRevoked.Should().BeTrue();
        oldEntity.ReplacedByTokenHash.Should().Be(Sha256(rotated.Value.RefreshToken));
    }

    [Fact]
    public async Task Reusing_an_already_rotated_token_is_rejected_and_revokes_the_whole_chain()
    {
        var initial = await _harness.AuthService.IssueTokensAsync(_userId, "127.0.0.1", "agent");
        var rotated = await _harness.AuthService.RefreshTokenAsync(initial.Value.RefreshToken, "127.0.0.1", "agent");

        // Replaying the already-rotated (now revoked) original token is reuse.
        var reuseAttempt = await _harness.AuthService.RefreshTokenAsync(initial.Value.RefreshToken, "10.0.0.9", "attacker-agent");

        reuseAttempt.IsFailure.Should().BeTrue();
        reuseAttempt.FirstError.Type.Should().Be(ErrorType.Unauthorized);

        // The legitimately-rotated token must also be revoked now, as a defensive measure.
        var followUp = await _harness.AuthService.RefreshTokenAsync(rotated.Value.RefreshToken, "127.0.0.1", "agent");
        followUp.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task An_expired_refresh_token_is_rejected()
    {
        var issued = await _harness.AuthService.IssueTokensAsync(_userId, "127.0.0.1", "agent");

        _harness.Clock.UtcNow = issued.Value.RefreshTokenExpiresAtUtc.AddSeconds(1);

        var result = await _harness.AuthService.RefreshTokenAsync(issued.Value.RefreshToken, "127.0.0.1", "agent");

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task A_revoked_refresh_token_cannot_be_used_to_refresh()
    {
        var issued = await _harness.AuthService.IssueTokensAsync(_userId, "127.0.0.1", "agent");

        await _harness.AuthService.LogoutAsync(_userId, issued.Value.RefreshToken);

        var result = await _harness.AuthService.RefreshTokenAsync(issued.Value.RefreshToken, "127.0.0.1", "agent");

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Unauthorized);
    }

    private static string Sha256(string raw) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
}
