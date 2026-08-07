using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Security;

/// <summary>
/// Milestone 17.2 - Data Protection key persistence and HTTP response
/// compression. QuerySplittingBehavior (the other M17.2 change) has no
/// black-box HTTP surface to assert on directly - it's covered by every
/// existing test that already exercises a multi-collection-include query
/// (e.g. `ProductDetailFlowTests`) continuing to return correct results
/// against the real SQL Server test database, plus manual verification that
/// the EF Core warning no longer appears in the dev server's own logs.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class DataProtectionAndPerformanceFlowTests
{
    private readonly AuthTestFixture _fixture;

    public DataProtectionAndPerformanceFlowTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Data_protection_keys_are_persisted_to_disk_not_left_ephemeral()
    {
        var client = _fixture.Factory.CreateClient();

        // Any request that renders _Layout.cshtml generates/uses an antiforgery
        // token, which is the Data Protection system's own first real use -
        // forcing key material to actually be created (not just registered).
        await client.GetAsync("/");

        var environment = _fixture.Factory.Services.GetRequiredService<IWebHostEnvironment>();
        var keyDirectory = Path.Combine(environment.ContentRootPath, "DataProtection-Keys");

        Directory.Exists(keyDirectory).Should().BeTrue("the configured key directory should have been created");
        Directory.GetFiles(keyDirectory, "key-*.xml").Should().NotBeEmpty("a real key file should have been written, not held only in memory");
    }

    [Fact]
    public async Task A_compressible_response_is_actually_compressed_when_the_client_supports_it()
    {
        var client = _fixture.Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Accept-Encoding", "gzip");

        var response = await client.SendAsync(request);

        response.Content.Headers.ContentEncoding.Should().Contain("gzip");
    }
}
