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

    /// <summary>
    /// Marks the request Approved - staff now expect the item(s) shipped
    /// back. Does not process a refund or restock inventory (Milestone
    /// 13.3's job, once the item is actually received).
    /// </summary>
    Task<Result> ApproveAsync(int returnRequestId, CancellationToken cancellationToken = default);

    Task<Result> RejectAsync(int returnRequestId, string rejectionReason, CancellationToken cancellationToken = default);
}
