namespace ECommerceApp.Domain.Security;

/// <summary>Role name constants, shared across layers without depending on Identity.</summary>
public static class Roles
{
    public const string SuperAdmin = nameof(SuperAdmin);
    public const string Admin = nameof(Admin);
    public const string CatalogManager = nameof(CatalogManager);
    public const string InventoryManager = nameof(InventoryManager);
    public const string OrderManager = nameof(OrderManager);
    public const string CustomerSupport = nameof(CustomerSupport);
    public const string Customer = nameof(Customer);

    public static readonly IReadOnlyList<string> All =
    [
        SuperAdmin, Admin, CatalogManager, InventoryManager, OrderManager, CustomerSupport, Customer,
    ];

    /// <summary>Comma-separated non-customer roles, for gating the Admin Area as a whole via <c>[Authorize(Roles = ...)]</c>.</summary>
    public const string StaffRolesCsv = $"{SuperAdmin},{Admin},{CatalogManager},{InventoryManager},{OrderManager},{CustomerSupport}";
}
