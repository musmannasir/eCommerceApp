using ECommerceApp.Application.Auth.Models;
using ECommerceApp.Application.Users.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using ECommerceApp.Infrastructure.Users;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Users;

public class UserManagementServiceTests : IAsyncLifetime
{
    private AuthServiceTestHarness _harness = null!;
    private UserManagementService _service = null!;

    public async Task InitializeAsync()
    {
        _harness = await AuthServiceTestHarness.CreateAsync();
        _service = new UserManagementService(_harness.DbContext, _harness.UserManager, _harness.Clock);
    }

    public Task DisposeAsync()
    {
        _harness.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> RegisterAsync(string email, string firstName = "Jane", string lastName = "Doe")
    {
        var result = await _harness.AuthService.RegisterAsync(
            new RegisterRequest(email, AuthServiceTestHarness.ValidPassword, firstName, lastName));
        return result.Value.UserId;
    }

    [Fact]
    public async Task GetPagedAsync_returns_every_user_with_their_role()
    {
        await RegisterAsync("customer1@example.com");

        var result = await _service.GetPagedAsync(new UserQuery());

        result.Value.Items.Should().ContainSingle(i => i.Email == "customer1@example.com" && i.Role == Roles.Customer);
    }

    [Fact]
    public async Task GetPagedAsync_search_matches_name_or_email()
    {
        await RegisterAsync("findme@example.com", "Unique", "Name");
        await RegisterAsync("other@example.com", "Other", "Person");

        var byEmail = await _service.GetPagedAsync(new UserQuery { Search = "findme" });
        var byName = await _service.GetPagedAsync(new UserQuery { Search = "Unique" });

        byEmail.Value.Items.Should().ContainSingle(i => i.Email == "findme@example.com");
        byName.Value.Items.Should().ContainSingle(i => i.Email == "findme@example.com");
    }

    [Fact]
    public async Task GetPagedAsync_filters_by_role()
    {
        var adminId = await RegisterAsync("admin-role@example.com");
        await _service.UpdateAsync(adminId, new UpdateUserRequest("Jane", "Doe", Roles.OrderManager), "some-other-admin-id");
        await RegisterAsync("plain-customer@example.com");

        var result = await _service.GetPagedAsync(new UserQuery { Role = Roles.OrderManager });

        result.Value.Items.Should().ContainSingle(i => i.Email == "admin-role@example.com");
    }

    [Fact]
    public async Task GetPagedAsync_filters_by_active_status()
    {
        var userId = await RegisterAsync("to-deactivate@example.com");
        await _service.DeactivateAsync(userId, "some-other-admin-id");

        var activeOnly = await _service.GetPagedAsync(new UserQuery { ActiveOnly = true });
        var inactiveOnly = await _service.GetPagedAsync(new UserQuery { ActiveOnly = false });

        activeOnly.Value.Items.Should().NotContain(i => i.Email == "to-deactivate@example.com");
        inactiveOnly.Value.Items.Should().ContainSingle(i => i.Email == "to-deactivate@example.com");
    }

    [Fact]
    public async Task CreateAsync_creates_a_user_with_the_given_role()
    {
        var result = await _service.CreateAsync(
            new CreateUserRequest("newstaff@example.com", AuthServiceTestHarness.ValidPassword, "New", "Staff", Roles.CustomerSupport),
            "admin-id");

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(Roles.CustomerSupport);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_email()
    {
        await RegisterAsync("dupe@example.com");

        var result = await _service.CreateAsync(
            new CreateUserRequest("dupe@example.com", AuthServiceTestHarness.ValidPassword, "New", "Staff", Roles.Customer),
            "admin-id");

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("user.duplicate_email");
    }

    [Fact]
    public async Task CreateAsync_rejects_an_unknown_role()
    {
        var result = await _service.CreateAsync(
            new CreateUserRequest("someone@example.com", AuthServiceTestHarness.ValidPassword, "New", "Staff", "NotARealRole"),
            "admin-id");

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("user.invalid_role");
    }

    [Fact]
    public async Task CreateAsync_writes_a_UserCreatedByAdmin_audit_event()
    {
        var result = await _service.CreateAsync(
            new CreateUserRequest("audited@example.com", AuthServiceTestHarness.ValidPassword, "New", "Staff", Roles.Customer),
            "admin-id");

        var events = _harness.DbContext.SecurityAuditEvents.Where(e => e.UserId == result.Value.Id).ToList();
        events.Should().ContainSingle(e => e.EventType == SecurityEventType.UserCreatedByAdmin);
    }

    [Fact]
    public async Task UpdateAsync_changes_the_role_and_writes_an_audit_event()
    {
        var userId = await RegisterAsync("promote@example.com");

        var result = await _service.UpdateAsync(userId, new UpdateUserRequest("Jane", "Doe", Roles.InventoryManager), "admin-id");

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(Roles.InventoryManager);
        var events = _harness.DbContext.SecurityAuditEvents.Where(e => e.UserId == userId).ToList();
        events.Should().ContainSingle(e => e.EventType == SecurityEventType.UserRoleChanged);
    }

    [Fact]
    public async Task UpdateAsync_rejects_an_admin_changing_their_own_role()
    {
        var userId = await RegisterAsync("self@example.com");

        var result = await _service.UpdateAsync(userId, new UpdateUserRequest("Jane", "Doe", Roles.SuperAdmin), actingAdminUserId: userId);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("user.cannot_change_own_role");
    }

    [Fact]
    public async Task UpdateAsync_allows_an_admin_to_edit_their_own_name_without_changing_role()
    {
        var userId = await RegisterAsync("selfname@example.com", "Old", "Name");

        var result = await _service.UpdateAsync(userId, new UpdateUserRequest("New", "Name", Roles.Customer), actingAdminUserId: userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("New");
    }

    [Fact]
    public async Task DeactivateAsync_then_ActivateAsync_round_trips_IsActive()
    {
        var userId = await RegisterAsync("toggle@example.com");

        var deactivate = await _service.DeactivateAsync(userId, "admin-id");
        var afterDeactivate = await _service.GetByIdAsync(userId);

        var activate = await _service.ActivateAsync(userId, "admin-id");
        var afterActivate = await _service.GetByIdAsync(userId);

        deactivate.IsSuccess.Should().BeTrue();
        afterDeactivate.Value.IsActive.Should().BeFalse();
        activate.IsSuccess.Should().BeTrue();
        afterActivate.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_rejects_an_admin_deactivating_their_own_account()
    {
        var userId = await RegisterAsync("selfdeactivate@example.com");

        var result = await _service.DeactivateAsync(userId, actingAdminUserId: userId);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("user.cannot_deactivate_self");
    }

    [Fact]
    public async Task UnlockAsync_clears_lockout_and_writes_an_audit_event()
    {
        var userId = await RegisterAsync("locked@example.com");
        var user = await _harness.UserManager.FindByIdAsync(userId);
        await _harness.UserManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(15));

        var result = await _service.UnlockAsync(userId, "admin-id");
        var detail = await _service.GetByIdAsync(userId);

        result.IsSuccess.Should().BeTrue();
        detail.Value.IsLockedOut.Should().BeFalse();
        var events = _harness.DbContext.SecurityAuditEvents.Where(e => e.UserId == userId).ToList();
        events.Should().ContainSingle(e => e.EventType == SecurityEventType.UserUnlocked);
    }

    [Fact]
    public async Task GetByIdAsync_returns_not_found_for_an_unknown_id()
    {
        var result = await _service.GetByIdAsync("no-such-id");

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("user.not_found");
    }
}
