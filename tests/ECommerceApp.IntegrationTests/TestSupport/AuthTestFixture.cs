using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.TestSupport;

public class AuthTestFixture : IAsyncLifetime
{
    public AuthWebApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new AuthWebApplicationFactory();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await TestDatabase.ResetAsync(dbContext);

        var seeder = scope.ServiceProvider.GetRequiredService<RoleAndAdminSeeder>();
        await seeder.SeedAsync();
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public class AuthTestCollection : ICollectionFixture<AuthTestFixture>
{
    public const string Name = "Auth integration tests";
}
