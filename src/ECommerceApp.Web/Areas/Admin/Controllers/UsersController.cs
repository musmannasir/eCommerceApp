using System.Security.Claims;
using ECommerceApp.Application.Auth;
using ECommerceApp.Application.Notifications;
using ECommerceApp.Application.Users;
using ECommerceApp.Application.Users.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Web.Areas.Admin.Models;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

/// <summary>
/// Milestone 16.1 - admin management of user accounts and role assignment.
/// Distinct from <see cref="Web.Controllers.AccountController"/>, which is
/// entirely self-service (register, log in, change own password).
/// </summary>
[Area("Admin")]
[Authorize(Policy = Policies.CanManageUsers)]
public class UsersController : Controller
{
    private readonly IUserManagementService _userManagementService;
    private readonly IAuthService _authService;
    private readonly IOutboxProcessor _outboxProcessor;

    public UsersController(IUserManagementService userManagementService, IAuthService authService, IOutboxProcessor outboxProcessor)
    {
        _userManagementService = userManagementService;
        _authService = authService;
        _outboxProcessor = outboxProcessor;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? role, bool? activeOnly, int page = 1)
    {
        var result = await _userManagementService.GetPagedAsync(new UserQuery
        {
            Page = page,
            Search = search,
            Role = role,
            ActiveOnly = activeOnly,
        });

        ViewData["Search"] = search;
        ViewData["Role"] = role;
        ViewData["ActiveOnly"] = activeOnly;
        ViewData["Roles"] = RoleSelectList(role);
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Roles"] = RoleSelectList(null);
        return View(new UserCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Roles"] = RoleSelectList(model.Role);
            return View(model);
        }

        var result = await _userManagementService.CreateAsync(
            new CreateUserRequest(model.Email, model.Password, model.FirstName, model.LastName, model.Role), CurrentUserId);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            ViewData["Roles"] = RoleSelectList(model.Role);
            return View(model);
        }

        TempData["Message"] = $"User '{model.Email}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var result = await _userManagementService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        ViewData["Roles"] = RoleSelectList(result.Value.Role);
        return View(ToEditViewModel(result.Value));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Roles"] = RoleSelectList(model.Role);
            return View(model);
        }

        var result = await _userManagementService.UpdateAsync(
            model.Id, new UpdateUserRequest(model.FirstName, model.LastName, model.Role), CurrentUserId);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            ViewData["Roles"] = RoleSelectList(model.Role);
            return View(model);
        }

        TempData["Message"] = "User updated.";
        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string id)
    {
        var result = await _userManagementService.ActivateAsync(id, CurrentUserId);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(string id)
    {
        var result = await _userManagementService.DeactivateAsync(id, CurrentUserId);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var result = await _userManagementService.UnlockAsync(id, CurrentUserId);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }
        else
        {
            TempData["Message"] = "Account unlocked.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>
    /// Reuses the exact same self-service flow <see cref="AccountController.ForgotPassword(Web.Models.Account.ForgotPasswordViewModel)"/>
    /// drives (Milestone 15.1/15.2) - an admin triggering it on someone
    /// else's behalf, rather than a new parallel implementation.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPasswordReset(string id)
    {
        var result = await _userManagementService.GetByIdAsync(id);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var email = result.Value.Email;
        await _authService.ForgotPasswordAsync(email, token =>
            Url.Action(nameof(AccountController.ResetPassword), "Account", new { email, token }, protocol: Request.Scheme)!);
        await _outboxProcessor.ProcessPendingAsync();

        TempData["Message"] = $"Password reset email sent to {email}.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private UserEditViewModel ToEditViewModel(UserDetailDto user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Role = user.Role,
        IsActive = user.IsActive,
        IsLockedOut = user.IsLockedOut,
        IsSelf = user.Id == CurrentUserId,
        CreatedAtUtc = user.CreatedAtUtc,
        LastSuccessfulLoginAtUtc = user.LastSuccessfulLoginAtUtc,
    };

    private static List<SelectListItem> RoleSelectList(string? selected) =>
        Roles.All.Select(r => new SelectListItem(r, r, r == selected)).ToList();

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
