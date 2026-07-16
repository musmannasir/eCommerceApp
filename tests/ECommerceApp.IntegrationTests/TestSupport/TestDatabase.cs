using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.IntegrationTests.TestSupport;

/// <summary>
/// The dedicated, real SQL Server database for automated integration tests
/// (never the dev or production database, per the project brief).
/// </summary>
public static class TestDatabase
{
    public const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=ECommerceAppTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    /// <summary>Applies pending migrations, then clears all auth-related tables for a clean slate.</summary>
    public static async Task ResetAsync(ApplicationDbContext dbContext)
    {
        await dbContext.Database.MigrateAsync();

        await dbContext.Database.ExecuteSqlRawAsync("""
            DELETE FROM RefreshTokens;
            DELETE FROM SecurityAuditEvents;
            DELETE FROM UserSessions;
            DELETE FROM AspNetUserRoles;
            DELETE FROM AspNetUserClaims;
            DELETE FROM AspNetUserLogins;
            DELETE FROM AspNetUserTokens;
            DELETE FROM AspNetRoleClaims;
            DELETE FROM AspNetUsers;
            DELETE FROM AspNetRoles;
            """);
    }
}
