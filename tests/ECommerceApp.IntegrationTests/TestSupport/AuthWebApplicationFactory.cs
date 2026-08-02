using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ECommerceApp.IntegrationTests.TestSupport;

public class AuthWebApplicationFactory : WebApplicationFactory<Web.Program>
{
    public const string SuperAdminEmail = "integration.superadmin@example.com";
    public const string SuperAdminPassword = "Sup3r!AdminPassw0rd";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestDatabase.ConnectionString,
                ["Jwt:Issuer"] = "ECommerceApp.IntegrationTests",
                ["Jwt:Audience"] = "ECommerceApp.IntegrationTests.Clients",
                ["Jwt:Key"] = "integration-test-signing-key-0123456789-abcdef-ghijk",
                ["SeedAdmin:Email"] = SuperAdminEmail,
                ["SeedAdmin:Password"] = SuperAdminPassword,
                // Functional tests exercise register/login repeatedly from the same loopback
                // address; a dedicated rate-limiting test would use its own factory with the
                // production default instead of raising it here.
                ["RateLimiting:AuthPermitLimit"] = "1000",
                ["RateLimiting:ReviewSubmissionPermitLimit"] = "1000",
                ["RateLimiting:ReviewReportPermitLimit"] = "1000",
            });
        });
    }
}
