using ECommerceApp.Infrastructure.Identity;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Security;

/// <summary>
/// Milestone 16.1 - admin user management end-to-end: authorization shape
/// (same CanManageUsers pattern as other admin-only screens), create/role-
/// change/activate/deactivate/unlock driving real HTTP against the real
/// storefront login flow, and the self-action guards that prevent an admin
/// from locking themselves out.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class UsersFlowTests
{
    private readonly AuthTestFixture _fixture;

    public UsersFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_user_list()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/Users/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("New user");
    }

    [Fact]
    public async Task Customer_cannot_manage_users()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.users.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/Users/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task OrderManager_cannot_manage_users()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"ordermanager.users.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "OrderManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Users/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task Admin_can_view_the_user_list()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"admin.users.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "Admin");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/Users/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("New user");
    }

    [Fact]
    public async Task SuperAdmin_can_create_a_user_with_a_staff_role_end_to_end()
    {
        var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var createPageHtml = await (await client.GetAsync("/Admin/Users/Create")).Content.ReadAsStringAsync();
        var token = HtmlHelpers.ExtractAntiForgeryToken(createPageHtml);
        var email = $"newstaff.{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsync("/Admin/Users/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Email"] = email,
            ["Password"] = "Str0ng!Passw0rd",
            ["FirstName"] = "New",
            ["LastName"] = "Staff",
            ["Role"] = "CustomerSupport",
        }));

        ((int)response.StatusCode).Should().BeInRange(300, 399);

        using var scope = _fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await userManager.FindByEmailAsync(email);
        created.Should().NotBeNull();
        (await userManager.GetRolesAsync(created!)).Should().Contain("CustomerSupport");

        // The account is immediately usable, exactly as if the admin had
        // handed the new hire their email and the password just set.
        var newStaffClient = _fixture.Factory.CreateClient();
        var loginResponse = await newStaffClient.LoginViaFormAsync(email, "Str0ng!Passw0rd");
        loginResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task SuperAdmin_can_change_a_users_role_end_to_end()
    {
        var targetEmail = $"rolechange.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(targetEmail, "Str0ng!Passw0rd", "Customer");

        var client = _fixture.Factory.CreateClient();
        await client.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        using var scope = _fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var target = await userManager.FindByEmailAsync(targetEmail);

        var editPageHtml = await client.GetStringAsync($"/Admin/Users/Edit?id={target!.Id}");
        var token = HtmlHelpers.ExtractAntiForgeryToken(editPageHtml);

        var response = await client.PostAsync("/Admin/Users/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Id"] = target.Id,
            ["FirstName"] = "Test",
            ["LastName"] = "Customer",
            ["Role"] = "OrderManager",
        }));

        response.IsSuccessStatusCode.Should().BeTrue();
        (await userManager.GetRolesAsync(target)).Should().Contain("OrderManager").And.NotContain("Customer");
    }

    [Fact]
    public async Task Deactivating_an_account_blocks_its_next_login_and_reactivating_restores_it()
    {
        var targetEmail = $"deactivateme.{Guid.NewGuid():N}@example.com";
        var targetClient = _fixture.Factory.CreateClient();
        await targetClient.RegisterViaFormAsync(targetEmail, "Str0ng!Passw0rd", "Will", "BeDeactivated");

        var adminClient = _fixture.Factory.CreateClient();
        await adminClient.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        using var scope = _fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var target = await userManager.FindByEmailAsync(targetEmail);

        var editPageHtml = await adminClient.GetStringAsync($"/Admin/Users/Edit?id={target!.Id}");
        var deactivateToken = HtmlHelpers.ExtractAntiForgeryToken(editPageHtml);
        await adminClient.PostAsync("/Admin/Users/Deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = deactivateToken,
            ["id"] = target.Id,
        }));

        var freshClientAfterDeactivation = _fixture.Factory.CreateClient();
        var blockedLogin = await freshClientAfterDeactivation.LoginViaFormAsync(targetEmail, "Str0ng!Passw0rd");
        var blockedBody = await blockedLogin.Content.ReadAsStringAsync();
        blockedBody.Should().Contain("Invalid email or password");

        var reactivatePageHtml = await adminClient.GetStringAsync($"/Admin/Users/Edit?id={target.Id}");
        var activateToken = HtmlHelpers.ExtractAntiForgeryToken(reactivatePageHtml);
        await adminClient.PostAsync("/Admin/Users/Activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = activateToken,
            ["id"] = target.Id,
        }));

        var freshClientAfterReactivation = _fixture.Factory.CreateClient();
        var restoredLogin = await freshClientAfterReactivation.LoginViaFormAsync(targetEmail, "Str0ng!Passw0rd");
        restoredLogin.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_or_change_the_role_of_their_own_account()
    {
        var email = $"selfguard.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "Admin");

        var client = _fixture.Factory.CreateClient();
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        using var scope = _fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var self = await userManager.FindByEmailAsync(email);

        var editPageHtml = await client.GetStringAsync($"/Admin/Users/Edit?id={self!.Id}");
        var deactivateToken = HtmlHelpers.ExtractAntiForgeryToken(editPageHtml);
        await client.PostAsync("/Admin/Users/Deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = deactivateToken,
            ["id"] = self.Id,
        }));

        var editToken = HtmlHelpers.ExtractAntiForgeryToken(editPageHtml);
        var roleChangeResponse = await client.PostAsync("/Admin/Users/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Id"] = self.Id,
            ["FirstName"] = "Test",
            ["LastName"] = "Admin",
            ["Role"] = "SuperAdmin",
        }));
        var roleChangeBody = await roleChangeResponse.Content.ReadAsStringAsync();

        using var verifyScope = _fixture.Factory.Services.CreateScope();
        var verifyUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var afterAttempts = await verifyUserManager.FindByIdAsync(self.Id);

        afterAttempts!.IsActive.Should().BeTrue("an admin cannot deactivate their own account");
        (await verifyUserManager.GetRolesAsync(afterAttempts)).Should().Contain("Admin").And.NotContain("SuperAdmin");
        roleChangeBody.Should().Contain("You cannot change your own role.");
    }

    [Fact]
    public async Task Unlocking_a_locked_out_account_lets_it_log_in_again()
    {
        var targetEmail = $"unlockme.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(targetEmail, "Str0ng!Passw0rd", "Customer");

        var attackerClient = _fixture.Factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            await attackerClient.LoginViaFormAsync(targetEmail, "WrongPassword!1");
        }

        var stillLockedOutLogin = await _fixture.Factory.CreateClient().LoginViaFormAsync(targetEmail, "Str0ng!Passw0rd");
        var stillLockedOutBody = await stillLockedOutLogin.Content.ReadAsStringAsync();
        stillLockedOutBody.Should().Contain("locked");

        var adminClient = _fixture.Factory.CreateClient();
        await adminClient.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        using var scope = _fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var target = await userManager.FindByEmailAsync(targetEmail);

        var editPageHtml = await adminClient.GetStringAsync($"/Admin/Users/Edit?id={target!.Id}");
        var unlockToken = HtmlHelpers.ExtractAntiForgeryToken(editPageHtml);
        await adminClient.PostAsync("/Admin/Users/Unlock", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = unlockToken,
            ["id"] = target.Id,
        }));

        var afterUnlockLogin = await _fixture.Factory.CreateClient().LoginViaFormAsync(targetEmail, "Str0ng!Passw0rd");
        afterUnlockLogin.IsSuccessStatusCode.Should().BeTrue();
    }
}
