using ECommerceApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.IntegrationTests.TestSupport;

/// <summary>
/// The dedicated, real SQL Server database for automated integration tests
/// (never the dev or production database, per the project brief).
/// </summary>
public static class TestDatabase
{
    /// <summary>
    /// The only database name <see cref="ResetAsync"/> will ever run its
    /// destructive script against - deliberately distinct from the dev
    /// database's own name (<c>ECommerceAppDb</c>, configured via User
    /// Secrets per README.md), so a typo or a future refactor that changes
    /// how the connection string is resolved can't silently point this at
    /// real data.
    /// </summary>
    public const string DatabaseName = "ECommerceAppTestDb";

    public const string ConnectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    /// <summary>
    /// Applies pending migrations, then clears all auth- and catalog-related
    /// tables for a clean slate. Milestone 18.1 - refuses to run the
    /// destructive part of this against anything other than
    /// <see cref="DatabaseName"/>, checked against the DbContext's actual
    /// resolved connection at call time rather than trusting that whatever
    /// wired it up got it right - the whole point is to catch a future
    /// mistake in that wiring before it reaches a real database, not to
    /// re-verify a constant against itself.
    /// </summary>
    public static async Task ResetAsync(ApplicationDbContext dbContext)
    {
        var resolvedDatabaseName = new SqlConnectionStringBuilder(dbContext.Database.GetConnectionString()).InitialCatalog;
        if (!string.Equals(resolvedDatabaseName, DatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to reset a database that is not the dedicated integration-test database. " +
                $"Resolved database name was '{resolvedDatabaseName}', expected '{DatabaseName}'. " +
                "This check exists specifically to stop a misconfigured test run from wiping the dev or " +
                "production database - see Testing-Guide.md.");
        }

        await dbContext.Database.MigrateAsync();

        await dbContext.Database.ExecuteSqlRawAsync("""
            DELETE FROM OutboxMessages;
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

            DELETE FROM Payments;
            DELETE FROM Refunds;
            DELETE FROM ReturnRequestItems;
            DELETE FROM ReturnRequests;
            DELETE FROM OrderItems;
            DELETE FROM Orders;

            DELETE FROM CartItems;
            DELETE FROM Carts;
            DELETE FROM Promotions;
            DELETE FROM TaxRates;
            DELETE FROM ShippingMethods;
            DELETE FROM Addresses;

            DELETE FROM GoodsReceiptItems;
            DELETE FROM GoodsReceipts;
            DELETE FROM PurchaseOrderItems;
            DELETE FROM PurchaseOrders;
            DELETE FROM InventoryReservations;
            DELETE FROM StockMovements;
            DELETE FROM StockAdjustments;
            DELETE FROM InventoryItems;
            DELETE FROM Warehouses;
            DELETE FROM SupplierProducts;
            DELETE FROM Suppliers;
            DELETE FROM HomePageBanners;

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
            DBCC CHECKIDENT ('OutboxMessages', RESEED, 0);
            DBCC CHECKIDENT ('Payments', RESEED, 0);
            DBCC CHECKIDENT ('Refunds', RESEED, 0);
            DBCC CHECKIDENT ('ReturnRequestItems', RESEED, 0);
            DBCC CHECKIDENT ('ReturnRequests', RESEED, 0);
            DBCC CHECKIDENT ('OrderItems', RESEED, 0);
            DBCC CHECKIDENT ('Orders', RESEED, 0);
            DBCC CHECKIDENT ('CartItems', RESEED, 0);
            DBCC CHECKIDENT ('Carts', RESEED, 0);
            DBCC CHECKIDENT ('Promotions', RESEED, 0);
            DBCC CHECKIDENT ('TaxRates', RESEED, 0);
            DBCC CHECKIDENT ('ShippingMethods', RESEED, 0);
            DBCC CHECKIDENT ('Addresses', RESEED, 0);
            DBCC CHECKIDENT ('InventoryReservations', RESEED, 0);
            DBCC CHECKIDENT ('StockMovements', RESEED, 0);
            DBCC CHECKIDENT ('StockAdjustments', RESEED, 0);
            DBCC CHECKIDENT ('InventoryItems', RESEED, 0);
            DBCC CHECKIDENT ('Warehouses', RESEED, 0);
            DBCC CHECKIDENT ('SupplierProducts', RESEED, 0);
            DBCC CHECKIDENT ('Suppliers', RESEED, 0);
            DBCC CHECKIDENT ('HomePageBanners', RESEED, 0);
            DBCC CHECKIDENT ('GoodsReceiptItems', RESEED, 0);
            DBCC CHECKIDENT ('GoodsReceipts', RESEED, 0);
            DBCC CHECKIDENT ('PurchaseOrderItems', RESEED, 0);
            DBCC CHECKIDENT ('PurchaseOrders', RESEED, 0);
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
