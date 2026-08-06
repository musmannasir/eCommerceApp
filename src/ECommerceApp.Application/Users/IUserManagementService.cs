using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Users.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Users;

/// <summary>
/// Milestone 16.1 - admin management of user accounts and role assignment,
/// as opposed to <see cref="Auth.IAuthService"/>'s self-service surface
/// (register, login, change own password). Every state-changing method
/// takes an acting admin's user id, both to guard against an admin
/// accidentally locking themselves out (self-deactivation, self-role-change
/// are rejected) and to record who made the change in the written
/// <see cref="Domain.Security.SecurityAuditEvent"/>.
/// </summary>
public interface IUserManagementService
{
    Task<Result<PagedResult<UserListItemDto>>> GetPagedAsync(UserQuery query, CancellationToken cancellationToken = default);

    Task<Result<UserDetailDto>> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<Result<UserDetailDto>> CreateAsync(CreateUserRequest request, string actingAdminUserId, CancellationToken cancellationToken = default);

    Task<Result<UserDetailDto>> UpdateAsync(string userId, UpdateUserRequest request, string actingAdminUserId, CancellationToken cancellationToken = default);

    Task<Result> ActivateAsync(string userId, string actingAdminUserId, CancellationToken cancellationToken = default);

    Task<Result> DeactivateAsync(string userId, string actingAdminUserId, CancellationToken cancellationToken = default);

    Task<Result> UnlockAsync(string userId, string actingAdminUserId, CancellationToken cancellationToken = default);
}
