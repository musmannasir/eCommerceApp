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

    Task<Result<PagedResult<StockMovementDto>>> GetMovementHistoryAsync(int inventoryItemId, PagedQuery query, CancellationToken cancellationToken = default);
}
