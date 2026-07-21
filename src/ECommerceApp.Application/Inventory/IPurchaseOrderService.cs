using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Inventory;

public interface IPurchaseOrderService
{
    Task<Result<PurchaseOrderDto>> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<Result<PurchaseOrderDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<PurchaseOrderListItemDto>>> GetPagedAsync(PurchaseOrderQuery query, CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderItemDto>> AddItemAsync(AddPurchaseOrderItemRequest request, CancellationToken cancellationToken = default);
    Task<Result> RemoveItemAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default);

    Task<Result> SubmitAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> ApproveAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> CancelAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<GoodsReceiptDto>> ReceiveAsync(ReceiveGoodsRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GoodsReceiptDto>>> GetReceiptHistoryAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
}
