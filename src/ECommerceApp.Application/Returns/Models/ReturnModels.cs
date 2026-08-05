using ECommerceApp.Application.Common.Models;
using ECommerceApp.Domain.Orders;

namespace ECommerceApp.Application.Returns.Models;

public record CreateReturnRequestItem(int OrderItemId, int Quantity);

public record CreateReturnRequestRequest(string OrderNumber, ReturnReason Reason, string? Comment, IReadOnlyList<CreateReturnRequestItem> Items);

public record ReturnRequestItemDto(int OrderItemId, string ProductName, int Quantity);

public record ReturnRequestDto(
    int Id,
    string OrderNumber,
    ReturnReason Reason,
    string? Comment,
    ReturnRequestStatus Status,
    DateTime RequestedAtUtc,
    string? RejectionReason,
    decimal? RefundedAmount,
    DateTime? RefundedAtUtc,
    IReadOnlyList<ReturnRequestItemDto> Items);

/// <summary>The admin return queue's row shape - deliberately lighter than ReturnRequestDto, the same relationship OrderListItemDto has to the full order.</summary>
public record ReturnRequestQueueItemDto(
    int Id,
    string OrderNumber,
    string CustomerName,
    ReturnReason Reason,
    string? Comment,
    DateTime RequestedAtUtc,
    IReadOnlyList<ReturnRequestItemDto> Items);

public record ReturnRequestQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
