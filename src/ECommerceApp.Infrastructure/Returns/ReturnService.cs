using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Inventory;
using ECommerceApp.Application.Payments;
using ECommerceApp.Application.Payments.Models;
using ECommerceApp.Application.Returns;
using ECommerceApp.Application.Returns.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Orders;
using ECommerceApp.Domain.Payments;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Returns;

/// <summary>
/// Queries ApplicationDbContext directly, the same convention every other
/// Storefront-adjacent service follows. Mirrors PurchaseOrderService's
/// request/approve/reject shape - load, check the current status, mutate,
/// save - rather than a shared state-machine table like OrderStatusTransitions,
/// since there are only four states and two real decision points (the
/// initial approve/reject, and Milestone 13.3's mark-received-and-refund).
/// </summary>
public sealed class ReturnService : IReturnService
{
    private static readonly ReturnRequestStatus[] OpenStatuses = { ReturnRequestStatus.Requested, ReturnRequestStatus.Approved };

    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentGateway _paymentGateway;

    public ReturnService(
        ApplicationDbContext dbContext,
        IClock clock,
        ICurrentUserService currentUserService,
        IInventoryService inventoryService,
        IPaymentGateway paymentGateway)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserService = currentUserService;
        _inventoryService = inventoryService;
        _paymentGateway = paymentGateway;
    }

    public async Task<Result<ReturnRequestDto>> SubmitReturnRequestAsync(string userId, CreateReturnRequestRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == request.OrderNumber && o.UserId == userId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<ReturnRequestDto>(Error.NotFound("return.order_not_found", "Order not found."));
        }

        if (order.Status != OrderStatus.Delivered)
        {
            return Result.Failure<ReturnRequestDto>(Error.Validation("return.not_eligible", "Only a delivered order can be returned."));
        }

        var hasOpenRequest = await _dbContext.ReturnRequests
            .AnyAsync(r => r.OrderId == order.Id && OpenStatuses.Contains(r.Status), cancellationToken);

        if (hasOpenRequest)
        {
            return Result.Failure<ReturnRequestDto>(Error.Conflict("return.already_open", "A return request for this order is already pending."));
        }

        if (request.Items.Count == 0)
        {
            return Result.Failure<ReturnRequestDto>(Error.Validation("return.no_items", "Select at least one item to return."));
        }

        var orderItemsById = order.Items.ToDictionary(i => i.Id);
        foreach (var item in request.Items)
        {
            if (!orderItemsById.TryGetValue(item.OrderItemId, out var orderItem))
            {
                return Result.Failure<ReturnRequestDto>(Error.Validation("return.invalid_item", "One of the selected items does not belong to this order."));
            }

            if (item.Quantity < 1 || item.Quantity > orderItem.Quantity)
            {
                return Result.Failure<ReturnRequestDto>(Error.Validation(
                    "return.invalid_quantity", $"Quantity for {orderItem.ProductName} must be between 1 and {orderItem.Quantity}."));
            }
        }

        var returnRequest = new ReturnRequest
        {
            OrderId = order.Id,
            UserId = userId,
            Reason = request.Reason,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment,
            Items = request.Items.Select(i => new ReturnRequestItem { OrderItemId = i.OrderItemId, Quantity = i.Quantity }).ToList(),
        };

        _dbContext.ReturnRequests.Add(returnRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(returnRequest, order.OrderNumber, orderItemsById, refund: null));
    }

    public async Task<IReadOnlyList<ReturnRequestDto>> GetReturnRequestsForOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return Array.Empty<ReturnRequestDto>();
        }

        var requests = await _dbContext.ReturnRequests
            .Include(r => r.Items)
            .Where(r => r.OrderId == orderId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        var requestIds = requests.Select(r => r.Id).ToList();
        var refundsByRequestId = await _dbContext.Refunds
            .Where(f => requestIds.Contains(f.ReturnRequestId))
            .ToDictionaryAsync(f => f.ReturnRequestId, cancellationToken);

        var orderItemsById = order.Items.ToDictionary(i => i.Id);
        return requests.Select(r => ToDto(r, order.OrderNumber, orderItemsById, refundsByRequestId.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<PagedResult<ReturnRequestQueueItemDto>> GetPendingQueueAsync(ReturnRequestQuery query, CancellationToken cancellationToken = default) =>
        await GetQueueByStatusAsync(ReturnRequestStatus.Requested, query, cancellationToken);

    public async Task<PagedResult<ReturnRequestQueueItemDto>> GetAwaitingReceiptQueueAsync(ReturnRequestQuery query, CancellationToken cancellationToken = default) =>
        await GetQueueByStatusAsync(ReturnRequestStatus.Approved, query, cancellationToken);

    private async Task<PagedResult<ReturnRequestQueueItemDto>> GetQueueByStatusAsync(ReturnRequestStatus status, ReturnRequestQuery query, CancellationToken cancellationToken)
    {
        var filtered = _dbContext.ReturnRequests
            .Where(r => r.Status == status);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var rows = await filtered
            .OrderBy(r => r.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new
            {
                r.Id,
                r.Reason,
                r.Comment,
                r.CreatedAtUtc,
                OrderNumber = r.Order.OrderNumber,
                CustomerName = r.Order.ShippingFullName,
                Items = r.Items.Select(i => new ReturnRequestItemDto(i.OrderItemId, i.OrderItem.ProductName, i.Quantity)).ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ReturnRequestQueueItemDto(r.Id, r.OrderNumber, r.CustomerName, r.Reason, r.Comment, r.CreatedAtUtc, r.Items))
            .ToList();

        return new PagedResult<ReturnRequestQueueItemDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<Result> ApproveAsync(int returnRequestId, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ReturnRequests.FirstOrDefaultAsync(r => r.Id == returnRequestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure(Error.NotFound("return.not_found", "Return request not found."));
        }

        if (request.Status != ReturnRequestStatus.Requested)
        {
            return Result.Failure(Error.Validation("return.invalid_transition", "Only a pending return request can be approved."));
        }

        request.Status = ReturnRequestStatus.Approved;
        request.DecidedAtUtc = _clock.UtcNow;
        request.DecidedByUserId = _currentUserService.UserId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(int returnRequestId, string rejectionReason, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ReturnRequests.FirstOrDefaultAsync(r => r.Id == returnRequestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure(Error.NotFound("return.not_found", "Return request not found."));
        }

        if (request.Status != ReturnRequestStatus.Requested)
        {
            return Result.Failure(Error.Validation("return.invalid_transition", "Only a pending return request can be rejected."));
        }

        request.Status = ReturnRequestStatus.Rejected;
        request.DecidedAtUtc = _clock.UtcNow;
        request.DecidedByUserId = _currentUserService.UserId;
        request.RejectionReason = rejectionReason;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RefundAsync(int returnRequestId, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ReturnRequests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == returnRequestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure(Error.NotFound("return.not_found", "Return request not found."));
        }

        if (request.Status != ReturnRequestStatus.Approved)
        {
            return Result.Failure(Error.Validation("return.invalid_transition", "Only an approved return request can be marked received."));
        }

        var order = await _dbContext.Orders.Include(o => o.Items).FirstAsync(o => o.Id == request.OrderId, cancellationToken);
        var orderItemsById = order.Items.ToDictionary(i => i.Id);

        var amount = request.Items.Sum(i => i.Quantity * orderItemsById[i.OrderItemId].UnitPrice);

        var refundResult = await _paymentGateway.RefundAsync(new RefundRequest(amount), cancellationToken);
        if (!refundResult.Succeeded)
        {
            return Result.Failure(Error.Validation("return.refund_failed", refundResult.FailureReason ?? "The refund could not be processed."));
        }

        // Restock each item at the exact warehouse it was originally
        // reserved from, found via the (now-consumed) reservation the order
        // made at checkout - a product that was untracked at order time (no
        // matching reservation) has nothing to restock, the same leniency
        // untracked inventory already gets everywhere else in this app.
        var reservations = await _dbContext.InventoryReservations
            .Include(r => r.InventoryItem)
            .Where(r => r.ReferenceType == "Order" && r.ReferenceId == order.Id.ToString())
            .ToListAsync(cancellationToken);

        foreach (var returnItem in request.Items)
        {
            var orderItem = orderItemsById[returnItem.OrderItemId];
            var reservation = reservations.FirstOrDefault(r =>
                r.InventoryItem.ProductId == orderItem.ProductId && r.InventoryItem.ProductVariantId == orderItem.ProductVariantId);

            if (reservation is not null)
            {
                await _inventoryService.RestockReturnedItemAsync(
                    reservation.InventoryItemId, returnItem.Quantity, request.Id, cancellationToken);
            }
        }

        var utcNow = _clock.UtcNow;
        _dbContext.Refunds.Add(new Refund
        {
            OrderId = order.Id,
            ReturnRequestId = request.Id,
            Amount = amount,
            ProcessedAtUtc = utcNow,
            ProcessedByUserId = _currentUserService.UserId,
        });

        request.Status = ReturnRequestStatus.Refunded;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static ReturnRequestDto ToDto(ReturnRequest request, string orderNumber, IReadOnlyDictionary<int, OrderItem> orderItemsById, Refund? refund)
    {
        var items = request.Items
            .Select(i => new ReturnRequestItemDto(i.OrderItemId, orderItemsById[i.OrderItemId].ProductName, i.Quantity))
            .ToList();

        return new ReturnRequestDto(
            request.Id, orderNumber, request.Reason, request.Comment, request.Status, request.CreatedAtUtc, request.RejectionReason,
            refund?.Amount, refund?.ProcessedAtUtc, items);
    }
}
