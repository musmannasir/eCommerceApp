using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ECommerceApp.IntegrationTests.TestSupport;

/// <summary>
/// Regression coverage for the Milestone 18.1 database-name safety guard in
/// <see cref="TestDatabase.ResetAsync"/> - proves it refuses to run its
/// destructive script against anything other than
/// <see cref="TestDatabase.DatabaseName"/> before a real connection is ever
/// opened (the guard reads the connection string, not the database itself).
/// </summary>
public class TestDatabaseSafetyGuardTests
{
    [Fact]
    public async Task ResetAsync_throws_instead_of_resetting_a_database_that_is_not_the_dedicated_test_database()
    {
        var options = new DbContextOptionsBuilder()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ECommerceAppDb;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(options, Mock.Of<ICurrentUserService>(), Mock.Of<IClock>());

        var act = () => TestDatabase.ResetAsync(dbContext);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Resolved database name was 'ECommerceAppDb'*");
    }
}
