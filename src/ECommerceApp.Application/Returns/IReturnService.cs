using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Returns.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Returns;

public interface IReturnService
{
    /// <summary>
    /// Only a Delivered order is eligible - there is no day-count return
    /// window anywhere in this app to enforce automatically. Fails with
    /// Conflict if an open (Requested/Approved) request already exists for
    /// this order, or Validation if no items/an invalid quantity is given.
    /// </summary>
    Task<Result<ReturnRequestDto>> SubmitReturnRequestAsync(string userId, CreateReturnRequestRequest request, CancellationToken cancellationToken = default);

    /// <summary>Every return request ever filed against this order, newest first - used to enrich OrderDto (Milestone 13.2), the same way IReviewService enriches ProductDetailDto (Milestone 12.1).</summary>
    Task<IReadOnlyList<ReturnRequestDto>> GetReturnRequestsForOrderAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>Admin return queue - every request still awaiting a decision (Status == Requested), oldest first.</summary>
    Task<PagedResult<ReturnRequestQueueItemDto>> GetPendingQueueAsync(ReturnRequestQuery query, CancellationToken cancellationToken = default);

    /// <summary>Admin queue for approved requests whose item(s) haven't been marked received yet (Status == Approved), oldest first - Milestone 13.3.</summary>
    Task<PagedResult<ReturnRequestQueueItemDto>> GetAwaitingReceiptQueueAsync(ReturnRequestQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the request Approved - staff now expect the item(s) shipped
    /// back. Does not process a refund or restock inventory (Milestone
    /// 13.3's job, once the item is actually received).
    /// </summary>
    Task<Result> ApproveAsync(int returnRequestId, CancellationToken cancellationToken = default);

    Task<Result> RejectAsync(int returnRequestId, string rejectionReason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an Approved request's item(s) as physically received back
    /// (Milestone 13.3) - refunds the returned items' line total (quantity
    /// times each OrderItem's UnitPrice; tax/shipping are not
    /// proportionally refunded) and restocks each item at the warehouse it
    /// was originally reserved from, in one step. Fails validation if the
    /// request isn't Approved, or if the refund itself is declined by the
    /// payment gateway (the request stays Approved so staff can retry).
    /// </summary>
    Task<Result> RefundAsync(int returnRequestId, CancellationToken cancellationToken = default);
}
