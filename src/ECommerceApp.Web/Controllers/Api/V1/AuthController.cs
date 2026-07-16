using System.Security.Claims;
using ECommerceApp.Application.Auth;
using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerceApp.Web.Controllers.Api.V1;

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string? RefreshToken);

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateOrNullAsync(_registerValidator, request, cancellationToken) is { } validationProblem)
        {
            return validationProblem;
        }

        var registerResult = await _authService.RegisterAsync(request, cancellationToken);
        if (registerResult.IsFailure)
        {
            return this.ToProblem(registerResult.FirstError);
        }

        var tokensResult = await _authService.IssueTokensAsync(
            registerResult.Value.UserId, RemoteIp(), Request.Headers.UserAgent.ToString(), cancellationToken);

        if (tokensResult.IsFailure)
        {
            return this.ToProblem(tokensResult.FirstError);
        }

        return Ok(new LoginResult(registerResult.Value, tokensResult.Value));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateOrNullAsync(_loginValidator, request, cancellationToken) is { } validationProblem)
        {
            return validationProblem;
        }

        var credentialsResult = await _authService.ValidateCredentialsAsync(
            request, LoginMethod.JwtApi, RemoteIp(), Request.Headers.UserAgent.ToString(), cancellationToken);

        if (credentialsResult.IsFailure)
        {
            return this.ToProblem(credentialsResult.FirstError);
        }

        var tokensResult = await _authService.IssueTokensAsync(
            credentialsResult.Value.UserId, RemoteIp(), Request.Headers.UserAgent.ToString(), cancellationToken);

        if (tokensResult.IsFailure)
        {
            return this.ToProblem(tokensResult.FirstError);
        }

        return Ok(new LoginResult(credentialsResult.Value, tokensResult.Value));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken, RemoteIp(), Request.Headers.UserAgent.ToString(), cancellationToken);

        return result.IsFailure ? this.ToProblem(result.FirstError) : Ok(result.Value);
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _authService.LogoutAsync(userId, request.RefreshToken, cancellationToken);

        return NoContent();
    }

    [HttpPost("revoke-all")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> RevokeAll(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _authService.RevokeAllSessionsAsync(userId, RemoteIp(), cancellationToken);

        return result.IsFailure ? this.ToProblem(result.FirstError) : NoContent();
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _authService.GetCurrentUserAsync(userId, cancellationToken);

        return result.IsFailure ? this.ToProblem(result.FirstError) : Ok(result.Value);
    }

    private string? RemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
