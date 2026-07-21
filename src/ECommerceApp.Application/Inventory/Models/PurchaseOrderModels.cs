namespace ECommerceApp.Application.Inventory.Models;

public record PurchaseOrderItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string ProductSku,
    int QuantityOrdered,
    int QuantityReceived,
    decimal UnitCost);

public record PurchaseOrderDto(
    int Id,
    string OrderNumber,
    int SupplierId,
    string SupplierName,
    int WarehouseId,
    string WarehouseName,
    string Status,
    DateTime? ExpectedDeliveryDate,
    string? Notes,
    DateTime? SubmittedAtUtc,
    DateTime? ApprovedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    IReadOnlyList<PurchaseOrderItemDto> Items,
    byte[] RowVersion);

public record PurchaseOrderListItemDto(
    int Id,
    string OrderNumber,
    string SupplierName,
    string WarehouseName,
    string Status,
    int ItemCount,
    decimal TotalCost,
    DateTime? ExpectedDeliveryDate);

public record CreatePurchaseOrderRequest(
    int SupplierId,
    int WarehouseId,
    DateTime? ExpectedDeliveryDate,
    string? Notes);

public record AddPurchaseOrderItemRequest(
    int PurchaseOrderId,
    int ProductId,
    int QuantityOrdered,
    decimal UnitCost);

public record PurchaseOrderQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? Status { get; init; }
}

public record ReceiveGoodsLineRequest(int PurchaseOrderItemId, int QuantityReceived, bool AllowOverride);

public record ReceiveGoodsRequest(
    int PurchaseOrderId,
    IReadOnlyList<ReceiveGoodsLineRequest> Lines,
    string? Notes,
    string? OverrideReason);

public record GoodsReceiptItemDto(int Id, int PurchaseOrderItemId, string ProductName, int QuantityReceived, bool IsOverride);

public record GoodsReceiptDto(
    int Id,
    int PurchaseOrderId,
    DateTime ReceivedAtUtc,
    string? ReceivedByUserId,
    string? Notes,
    string? OverrideReason,
    IReadOnlyList<GoodsReceiptItemDto> Items);
