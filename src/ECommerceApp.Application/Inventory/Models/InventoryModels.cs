namespace ECommerceApp.Application.Inventory.Models;

public record WarehouseDto(
    int Id,
    string Name,
    string Code,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    bool IsDefault,
    bool IsActive);

public record CreateWarehouseRequest(
    string Name,
    string Code,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    bool IsDefault,
    bool IsActive);

public record UpdateWarehouseRequest(
    int Id,
    string Name,
    string Code,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    bool IsDefault,
    bool IsActive);

public record InventoryItemDto(
    int Id,
    int WarehouseId,
    string WarehouseName,
    int ProductId,
    string ProductName,
    int? ProductVariantId,
    string Sku,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    int ReorderLevel,
    int ReorderQuantity,
    bool AllowBackorder,
    string StockStatus,
    DateTime LastStockUpdateUtc,
    byte[] RowVersion);

public record RecordOpeningStockRequest(
    int WarehouseId,
    int ProductId,
    int? ProductVariantId,
    int Quantity,
    int ReorderLevel,
    int ReorderQuantity,
    bool AllowBackorder);

public record AdjustStockRequest(int InventoryItemId, int QuantityDelta, string Reason);

public record ReserveStockRequest(int InventoryItemId, int Quantity, string? ReferenceType, string? ReferenceId);

public record InventoryReservationDto(
    int Id,
    int InventoryItemId,
    int Quantity,
    string Status,
    string? ReferenceType,
    string? ReferenceId,
    DateTime? ExpiresAtUtc,
    DateTime? ReleasedAtUtc);

public record StockMovementDto(
    int Id,
    int InventoryItemId,
    string MovementType,
    int QuantityChange,
    int QuantityOnHandAfter,
    int QuantityReservedAfter,
    string? ReferenceType,
    int? ReferenceId,
    string? Reason,
    DateTime OccurredAtUtc,
    string? CreatedByUserId);

public record InventoryItemQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int? WarehouseId { get; init; }
    public string? Search { get; init; }
    public bool OnlyLowStock { get; init; }
    public bool OnlyOutOfStock { get; init; }
}
