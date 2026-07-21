namespace ECommerceApp.Domain.Inventory;

public enum StockMovementType
{
    OpeningStock,
    PurchaseReceipt,
    SaleReservation,
    SaleCompletion,
    ReservationRelease,
    CustomerReturn,
    SupplierReturn,
    Damage,
    Loss,
    ManualAdjustment,
    Transfer,
}
