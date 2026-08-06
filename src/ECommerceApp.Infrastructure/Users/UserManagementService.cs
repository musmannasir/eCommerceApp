using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Users;
using ECommerceApp.Application.Users.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Identity;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Users;

/// <summary>
/// Milestone 16.1. Reads go through <see cref="ApplicationDbContext"/>
/// directly (this app's usual convention), except for a single user's role
/// lookup/change which uses <see cref="UserManager{TUser}"/>'s own
/// higher-level API - the same split <see cref="Security.AuthService"/>
/// already makes. The list query joins <c>UserRoles</c>/<c>Roles</c> once
/// as a simple, non-correlated query and finishes filtering/paging in
/// memory - the same "materialize then process" approach
/// <c>FinanceService.GetLedgerAsync</c> (Milestone 14.1) established, to
/// stay identical across the EF Core InMemory (unit tests) and SQL Server
/// (real) providers.
/// </summary>
public sealed class UserManagementService : IUserManagementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClock _clock;

    public UserManagementService(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IClock clock)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _clock = clock;
    }

    public async Task<Result<PagedResult<UserListItemDto>>> GetPagedAsync(UserQuery query, CancellationToken cancellationToken = default)
    {
        var usersQuery = _dbContext.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            usersQuery = usersQuery.Where(u =>
                u.Email!.Contains(query.Search) || u.FirstName.Contains(query.Search) || u.LastName.Contains(query.Search));
        }

        if (query.ActiveOnly.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.IsActive == query.ActiveOnly.Value);
        }

        var users = await usersQuery.OrderBy(u => u.Email).ToListAsync(cancellationToken);

        var userRoles = await (
            from ur in _dbContext.UserRoles
            join r in _dbContext.Roles on ur.RoleId equals r.Id
            select new { ur.UserId, r.Name }
        ).ToListAsync(cancellationToken);
        var roleByUserId = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().Name ?? Roles.Customer);

        var withRoles = users.Select(u => (User: u, Role: roleByUserId.GetValueOrDefault(u.Id, Roles.Customer))).ToList();

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            withRoles = withRoles.Where(x => x.Role == query.Role).ToList();
        }

        var totalCount = withRoles.Count;
        var page = withRoles.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        var items = page.Select(x => ToListItemDto(x.User, x.Role)).ToList();

        return Result.Success(new PagedResult<UserListItemDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<UserDetailDto>> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure<UserDetailDto>(Error.NotFound("user.not_found", "User not found."));
        }

        return Result.Success(await ToDetailDtoAsync(user));
    }

    public async Task<Result<UserDetailDto>> CreateAsync(CreateUserRequest request, string actingAdminUserId, CancellationToken cancellationToken = default)
    {
        if (!Roles.All.Contains(request.Role))
        {
            return Result.Failure<UserDetailDto>(Error.Validation("user.invalid_role", "Unknown role."));
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Result.Failure<UserDetailDto>(Error.Conflict("user.duplicate_email", "An account with this email already exists."));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow,
            PasswordChangedAtUtc = _clock.UtcNow,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(" ", createResult.Errors.Select(e => e.Description));
            return Result.Failure<UserDetailDto>(Error.Validation("user.create_failed", errors));
        }

        await _userManager.AddToRoleAsync(user, request.Role);

        AddAuditEvent(user.Id, SecurityEventType.UserCreatedByAdmin, $"Created by admin {actingAdminUserId} with role {request.Role}.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await ToDetailDtoAsync(user));
    }

    public async Task<Result<UserDetailDto>> UpdateAsync(string userId, UpdateUserRequest request, string actingAdminUserId, CancellationToken cancellationToken = default)
    {
        if (!Roles.All.Contains(request.Role))
        {
            return Result.Failure<UserDetailDto>(Error.Validation("user.invalid_role", "Unknown role."));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure<UserDetailDto>(Error.NotFound("user.not_found", "User not found."));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var currentRole = currentRoles.FirstOrDefault() ?? Roles.Customer;

        if (userId == actingAdminUserId && request.Role != currentRole)
        {
            return Result.Failure<UserDetailDto>(Error.Validation("user.cannot_change_own_role", "You cannot change your own role."));
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        await _userManager.UpdateAsync(user);

        if (request.Role != currentRole)
        {
            if (currentRoles.Count > 0)
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            await _userManager.AddToRoleAsync(user, request.Role);

            AddAuditEvent(user.Id, SecurityEventType.UserRoleChanged, $"Role changed from {currentRole} to {request.Role} by admin {actingAdminUserId}.");
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(await ToDetailDtoAsync(user));
    }

    public async Task<Result> ActivateAsync(string userId, string actingAdminUserId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("user.not_found", "User not found."));
        }

        user.IsActive = true;
        await _userManager.UpdateAsync(user);

        AddAuditEvent(user.Id, SecurityEventType.UserActivated, $"Activated by admin {actingAdminUserId}.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(string userId, string actingAdminUserId, CancellationToken cancellationToken = default)
    {
        if (userId == actingAdminUserId)
        {
            return Result.Failure(Error.Validation("user.cannot_deactivate_self", "You cannot deactivate your own account."));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("user.not_found", "User not found."));
        }

        user.IsActive = false;
        await _userManager.UpdateAsync(user);

        AddAuditEvent(user.Id, SecurityEventType.UserDeactivated, $"Deactivated by admin {actingAdminUserId}.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UnlockAsync(string userId, string actingAdminUserId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("user.not_found", "User not found."));
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        AddAuditEvent(user.Id, SecurityEventType.UserUnlocked, $"Unlocked by admin {actingAdminUserId}.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private void AddAuditEvent(string userId, SecurityEventType eventType, string details)
    {
        _dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            UserId = userId,
            EventType = eventType,
            Succeeded = true,
            OccurredAtUtc = _clock.UtcNow,
            Details = details,
        });
    }

    private async Task<UserDetailDto> ToDetailDtoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var isLockedOut = await _userManager.IsLockedOutAsync(user);

        return new UserDetailDto(
            user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName,
            roles.FirstOrDefault() ?? Roles.Customer, user.IsActive, user.CreatedAtUtc,
            user.LastSuccessfulLoginAtUtc, user.PasswordChangedAtUtc, isLockedOut);
    }

    private static UserListItemDto ToListItemDto(ApplicationUser user, string role) => new(
        user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName,
        role, user.IsActive, user.CreatedAtUtc, user.LastSuccessfulLoginAtUtc);
}
