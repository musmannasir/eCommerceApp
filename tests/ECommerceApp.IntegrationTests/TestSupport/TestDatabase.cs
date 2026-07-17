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

    /// <summary>Applies pending migrations, then clears all auth- and catalog-related tables for a clean slate.</summary>
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

            DELETE FROM ProductVariantAttributeValues;
            DELETE FROM ProductImages;
            DELETE FROM ProductVariants;
            DELETE FROM ProductSpecifications;
            DELETE FROM ProductTagMappings;
            DELETE FROM ProductTags;
            DELETE FROM ProductAttributeValues;
            DELETE FROM ProductAttributes;
            DELETE FROM Products;
            UPDATE Categories SET ParentCategoryId = NULL;
            DELETE FROM Categories;
            DELETE FROM Brands;

            -- DELETE does not reset IDENTITY seeds (unlike TRUNCATE, which these FKs block);
            -- reset them explicitly so ids stay small and predictable across repeated local runs.
            DBCC CHECKIDENT ('ProductVariantAttributeValues', RESEED, 0);
            DBCC CHECKIDENT ('ProductImages', RESEED, 0);
            DBCC CHECKIDENT ('ProductVariants', RESEED, 0);
            DBCC CHECKIDENT ('ProductSpecifications', RESEED, 0);
            DBCC CHECKIDENT ('ProductTagMappings', RESEED, 0);
            DBCC CHECKIDENT ('ProductTags', RESEED, 0);
            DBCC CHECKIDENT ('ProductAttributeValues', RESEED, 0);
            DBCC CHECKIDENT ('ProductAttributes', RESEED, 0);
            DBCC CHECKIDENT ('Products', RESEED, 0);
            DBCC CHECKIDENT ('Categories', RESEED, 0);
            DBCC CHECKIDENT ('Brands', RESEED, 0);
            """);
    }
}
