namespace ECommerceApp.Domain.Security;

/// <summary>Authorization policy name constants. Policy-to-role mapping is configured in Web's DI.</summary>
public static class Policies
{
    public const string CanManageCatalog = nameof(CanManageCatalog);
    public const string CanManageInventory = nameof(CanManageInventory);
    public const string CanManageOrders = nameof(CanManageOrders);
    public const string CanManageUsers = nameof(CanManageUsers);
    public const string CanViewFinancialReports = nameof(CanViewFinancialReports);
    public const string CanProcessRefunds = nameof(CanProcessRefunds);
}
