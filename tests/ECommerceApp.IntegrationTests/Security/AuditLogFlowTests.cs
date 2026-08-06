using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.IntegrationTests.Security;

/// <summary>
/// Milestone 16.2 - the audit log viewer over the security audit trail
/// that's existed (write-only) since Milestone 1.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class AuditLogFlowTests
{
    private readonly AuthTestFixture _fixture;

    public AuditLogFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Anonymous_request_is_redirected_to_login_instead_of_the_audit_log()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/Admin/AuditLog/Index");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain("Log in").And.NotContain("Export CSV");
    }

    [Fact]
    public async Task Customer_cannot_view_the_audit_log()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"customer.auditlog.{Guid.NewGuid():N}@example.com";
        await client.RegisterViaFormAsync(email, "Str0ng!Passw0rd", "Test", "Customer");

        var response = await client.GetAsync("/Admin/AuditLog/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task OrderManager_cannot_view_the_audit_log()
    {
        var client = _fixture.Factory.CreateClient();
        var email = $"ordermanager.auditlog.{Guid.NewGuid():N}@example.com";
        await _fixture.Factory.CreateUserInRoleAsync(email, "Str0ng!Passw0rd", "OrderManager");
        await client.LoginViaFormAsync(email, "Str0ng!Passw0rd");

        var response = await client.GetAsync("/Admin/AuditLog/Index");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task A_real_registration_produces_a_visible_audit_entry()
    {
        var targetEmail = $"auditedregister.{Guid.NewGuid():N}@example.com";
        var registeringClient = _fixture.Factory.CreateClient();
        await registeringClient.RegisterViaFormAsync(targetEmail, "Str0ng!Passw0rd", "Audited", "Customer");

        var adminClient = _fixture.Factory.CreateClient();
        await adminClient.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var body = await adminClient.GetStringAsync($"/Admin/AuditLog/Index?search={Uri.EscapeDataString(targetEmail)}");

        body.Should().Contain(targetEmail);
        body.Should().Contain("RegisterSuccess");
    }

    [Fact]
    public async Task CSV_export_returns_a_real_transaction_for_the_current_filters()
    {
        var targetEmail = $"auditedexport.{Guid.NewGuid():N}@example.com";
        var registeringClient = _fixture.Factory.CreateClient();
        await registeringClient.RegisterViaFormAsync(targetEmail, "Str0ng!Passw0rd", "Audited", "Export");

        var adminClient = _fixture.Factory.CreateClient();
        await adminClient.LoginViaFormAsync(AuthWebApplicationFactory.SuperAdminEmail, AuthWebApplicationFactory.SuperAdminPassword);

        var response = await adminClient.GetAsync($"/Admin/AuditLog/ExportCsv?search={Uri.EscapeDataString(targetEmail)}");
        var csv = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        csv.Should().Contain(targetEmail);
        csv.Should().Contain("RegisterSuccess");
    }
}
