using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Returns.Models;

namespace ECommerceApp.Web.Models.Returns;

/// <summary>Composes the admin Returns page's two independent, independently-paginated sections (Milestone 13.3).</summary>
public record ReturnsQueueViewModel(
    PagedResult<ReturnRequestQueueItemDto> Pending,
    PagedResult<ReturnRequestQueueItemDto> AwaitingReceipt);
