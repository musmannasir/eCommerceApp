using System.Security.Claims;
using ECommerceApp.Application.Auth;
using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Identity;
using ECommerceApp.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerceApp.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthService authService,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ILogger<AccountController> logger)
    {
        _authService = authService;
        _signInManager = signInManager;
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.RegisterAsync(new RegisterRequest(model.Email, model.Password, model.FirstName, model.LastName));
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(result.Value.UserId);
        await _signInManager.SignInAsync(user!, isPersistent: false);

        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.ValidateCredentialsAsync(
            new LoginRequest(model.Email, model.Password),
            LoginMethod.CookieMvc,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(result.Value.UserId);
        await _signInManager.SignInAsync(user!, isPersistent: model.RememberMe);

        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _signInManager.SignOutAsync();
        await _authService.LogoutAsync(userId, rawRefreshToken: null);

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.ForgotPasswordAsync(model.Email);

        if (result.Value is { } token)
        {
            var resetLink = Url.Action(nameof(ResetPassword), "Account",
                new { email = model.Email, token }, protocol: Request.Scheme)!;

            await _emailSender.SendAsync(
                model.Email,
                "Reset your password",
                $"<p>Click <a href=\"{resetLink}\">this link</a> to reset your password. If you didn't request this, you can ignore this email.</p>");
        }

        // Always show the same confirmation, whether or not the email is registered.
        return View("ForgotPasswordConfirmation");
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest(model.Email, model.Token, model.NewPassword));
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        return View("ResetPasswordConfirmation");
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _authService.ChangePasswordAsync(new ChangePasswordRequest(userId, model.CurrentPassword, model.NewPassword));
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        // The password change rotated the Identity security stamp; refresh the cookie now
        // so this session doesn't get invalidated on its next security-stamp check.
        var user = await _userManager.GetUserAsync(User);
        await _signInManager.RefreshSignInAsync(user!);

        return View("ChangePasswordConfirmation");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _authService.GetCurrentUserAsync(userId);

        return View(result.Value);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _authService.RevokeAllSessionsAsync(userId, HttpContext.Connection.RemoteIpAddress?.ToString());

        await _signInManager.SignOutAsync();

        TempData["Message"] = "All sessions have been revoked. Please log in again.";
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (!string.IsNullOrEmpty(returnUrl))
        {
            _logger.LogWarning("Blocked a non-local returnUrl after authentication: {ReturnUrl}", returnUrl);
        }

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
