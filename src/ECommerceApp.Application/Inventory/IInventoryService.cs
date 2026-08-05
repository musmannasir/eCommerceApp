using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Inventory;

public interface IInventoryService
{
    Task<Result<WarehouseDto>> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken cancellationToken = default);
    Task<Result<WarehouseDto>> UpdateWarehouseAsync(UpdateWarehouseRequest request, CancellationToken cancellationToken = default);
    Task<Result<WarehouseDto>> GetWarehouseByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<WarehouseDto>>> GetWarehousesAsync(bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<Result> DeactivateWarehouseAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> ActivateWarehouseAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<InventoryItemDto>> RecordOpeningStockAsync(RecordOpeningStockRequest request, CancellationToken cancellationToken = default);
    Task<Result<InventoryItemDto>> GetInventoryItemByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<InventoryItemDto>>> GetOverviewAsync(InventoryItemQuery query, CancellationToken cancellationToken = default);

    Task<Result<InventoryItemDto>> AdjustStockAsync(AdjustStockRequest request, CancellationToken cancellationToken = default);

    Task<Result<InventoryReservationDto>> ReserveStockAsync(ReserveStockRequest request, CancellationToken cancellationToken = default);
    Task<Result> ReleaseReservationAsync(int reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deducts an active reservation's stock from on-hand
    /// (Milestone 10.3, shipping) rather than returning it to available -
    /// the item has physically left the warehouse. Unlike ReleaseReservationAsync,
    /// QuantityAvailable is unchanged by this (it already excluded the
    /// reserved quantity); only QuantityOnHand actually decreases.
    /// </summary>
    Task<Result> ConsumeReservationAsync(int reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The mirror image of <see cref="ConsumeReservationAsync"/> - adds a
    /// physically-returned item's quantity back to on-hand and records a
    /// CustomerReturn movement (Milestone 13.3). There is no reservation to
    /// touch here (the sale already completed at ship time), so this simply
    /// increases QuantityOnHand.
    /// </summary>
    Task<Result<InventoryItemDto>> RestockReturnedItemAsync(int inventoryItemId, int quantity, int returnRequestId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<StockMovementDto>>> GetMovementHistoryAsync(int inventoryItemId, PagedQuery query, CancellationToken cancellationToken = default);
}
