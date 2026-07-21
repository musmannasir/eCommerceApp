using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Inventory;
using ECommerceApp.Application.Inventory.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Inventory;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerceApp.Infrastructure.Inventory;

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUserService;

    public PurchaseOrderService(ApplicationDbContext dbContext, IClock clock, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PurchaseOrderDto>> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            return Result.Failure<PurchaseOrderDto>(Error.NotFound("purchase_order.supplier_not_found", "Supplier not found."));
        }

        if (!await _dbContext.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken))
        {
            return Result.Failure<PurchaseOrderDto>(Error.NotFound("purchase_order.warehouse_not_found", "Warehouse not found."));
        }

        var order = new PurchaseOrder
        {
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            Status = PurchaseOrderStatus.Draft,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Notes = request.Notes,
            OrderNumber = string.Empty,
        };

        _dbContext.PurchaseOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        order.OrderNumber = $"PO-{order.Id:D6}";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDtoAsync(order.Id, cancellationToken));
    }

    public async Task<Result<PurchaseOrderDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.PurchaseOrders.AnyAsync(p => p.Id == id, cancellationToken);
        return exists
            ? Result.Success(await MapToDtoAsync(id, cancellationToken))
            : Result.Failure<PurchaseOrderDto>(Error.NotFound("purchase_order.not_found", "Purchase order not found."));
    }

    public async Task<Result<PagedResult<PurchaseOrderListItemDto>>> GetPagedAsync(PurchaseOrderQuery query, CancellationToken cancellationToken = default)
    {
        var orders = _dbContext.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            orders = orders.Where(p => p.OrderNumber.Contains(query.Search) || p.Supplier.Name.Contains(query.Search));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<PurchaseOrderStatus>(query.Status, out var status))
        {
            orders = orders.Where(p => p.Status == status);
        }

        orders = orders.OrderByDescending(p => p.Id);

        var totalCount = await orders.CountAsync(cancellationToken);
        var page = await orders
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var items = page.Select(p => new PurchaseOrderListItemDto(
            p.Id, p.OrderNumber, p.Supplier.Name, p.Warehouse.Name, p.Status.ToString(),
            p.Items.Count, p.Items.Sum(i => i.UnitCost * i.QuantityOrdered), p.ExpectedDeliveryDate)).ToList();

        return Result.Success(new PagedResult<PurchaseOrderListItemDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<PurchaseOrderItemDto>> AddItemAsync(AddPurchaseOrderItemRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<PurchaseOrderItemDto>(Error.NotFound("purchase_order.not_found", "Purchase order not found."));
        }

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            return Result.Failure<PurchaseOrderItemDto>(Error.Validation(
                "purchase_order.not_draft", "Items can only be added while the purchase order is in Draft status."));
        }

        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<PurchaseOrderItemDto>(Error.NotFound("purchase_order.product_not_found", "Product not found."));
        }

        var item = new PurchaseOrderItem
        {
            PurchaseOrderId = order.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            ProductSku = product.BaseSKU,
            QuantityOrdered = request.QuantityOrdered,
            QuantityReceived = 0,
            UnitCost = request.UnitCost,
        };

        _dbContext.PurchaseOrderItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(item));
    }

    public async Task<Result> RemoveItemAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.PurchaseOrderItems
            .Include(i => i.PurchaseOrder)
            .FirstOrDefaultAsync(i => i.Id == purchaseOrderItemId, cancellationToken);
        if (item is null)
        {
            return Result.Failure(Error.NotFound("purchase_order.item_not_found", "Purchase order item not found."));
        }

        if (item.PurchaseOrder.Status != PurchaseOrderStatus.Draft)
        {
            return Result.Failure(Error.Validation(
                "purchase_order.not_draft", "Items can only be removed while the purchase order is in Draft status."));
        }

        _dbContext.PurchaseOrderItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SubmitAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (order is null)
        {
            return Result.Failure(Error.NotFound("purchase_order.not_found", "Purchase order not found."));
        }

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            return Result.Failure(Error.Validation("purchase_order.invalid_transition", "Only a Draft purchase order can be submitted."));
        }

        if (order.Items.Count == 0)
        {
            return Result.Failure(Error.Validation("purchase_order.no_items", "Add at least one item before submitting."));
        }

        order.Status = PurchaseOrderStatus.Submitted;
        order.SubmittedAtUtc = _clock.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ApproveAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (order is null)
        {
            return Result.Failure(Error.NotFound("purchase_order.not_found", "Purchase order not found."));
        }

        if (order.Status != PurchaseOrderStatus.Submitted)
        {
            return Result.Failure(Error.Validation("purchase_order.invalid_transition", "Only a Submitted purchase order can be approved."));
        }

        order.Status = PurchaseOrderStatus.Approved;
        order.ApprovedAtUtc = _clock.UtcNow;
        order.ApprovedByUserId = _currentUserService.UserId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (order is null)
        {
            return Result.Failure(Error.NotFound("purchase_order.not_found", "Purchase order not found."));
        }

        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted or PurchaseOrderStatus.Approved))
        {
            return Result.Failure(Error.Validation(
                "purchase_order.invalid_transition",
                "Only a Draft, Submitted, or Approved purchase order can be cancelled - once any goods have been received, it can no longer be cancelled outright."));
        }

        order.Status = PurchaseOrderStatus.Cancelled;
        order.CancelledAtUtc = _clock.UtcNow;
        order.CancelledByUserId = _currentUserService.UserId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<GoodsReceiptDto>> ReceiveAsync(ReceiveGoodsRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.PurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<GoodsReceiptDto>(Error.NotFound("purchase_order.not_found", "Purchase order not found."));
        }

        if (order.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
        {
            return Result.Failure<GoodsReceiptDto>(Error.Validation(
                "purchase_order.invalid_transition", "Only an Approved or PartiallyReceived purchase order can receive goods."));
        }

        var overrideByLine = new Dictionary<int, bool>();
        foreach (var line in request.Lines)
        {
            var item = order.Items.FirstOrDefault(i => i.Id == line.PurchaseOrderItemId);
            if (item is null)
            {
                return Result.Failure<GoodsReceiptDto>(Error.NotFound(
                    "purchase_order.item_not_found", "One of the submitted lines does not belong to this purchase order."));
            }

            var outstanding = item.QuantityOrdered - item.QuantityReceived;
            var isOverride = line.QuantityReceived > outstanding;
            overrideByLine[line.PurchaseOrderItemId] = isOverride;

            if (isOverride && !line.AllowOverride)
            {
                return Result.Failure<GoodsReceiptDto>(Error.Validation(
                    "purchase_order.over_receipt",
                    $"Cannot receive {line.QuantityReceived} for '{item.ProductName}' - only {outstanding} outstanding. Use the override to receive more than ordered."));
            }

            if (isOverride && string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                return Result.Failure<GoodsReceiptDto>(Error.Validation(
                    "purchase_order.override_reason_required", "An override reason is required when receiving more than the outstanding quantity."));
            }
        }

        var utcNow = _clock.UtcNow;
        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);

        var hasOverride = overrideByLine.Values.Any(isOverride => isOverride);
        var receipt = new GoodsReceipt
        {
            PurchaseOrderId = order.Id,
            ReceivedAtUtc = utcNow,
            ReceivedByUserId = _currentUserService.UserId,
            Notes = request.Notes,
            OverrideReason = hasOverride ? request.OverrideReason : null,
        };
        _dbContext.GoodsReceipts.Add(receipt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var line in request.Lines)
        {
            var item = order.Items.First(i => i.Id == line.PurchaseOrderItemId);
            var isOverride = overrideByLine[line.PurchaseOrderItemId];

            item.QuantityReceived += line.QuantityReceived;

            _dbContext.GoodsReceiptItems.Add(new GoodsReceiptItem
            {
                GoodsReceiptId = receipt.Id,
                PurchaseOrderItemId = item.Id,
                QuantityReceived = line.QuantityReceived,
                IsOverride = isOverride,
            });

            var inventoryItem = await _dbContext.InventoryItems.FirstOrDefaultAsync(
                i => i.WarehouseId == order.WarehouseId && i.ProductId == item.ProductId && i.ProductVariantId == null, cancellationToken);
            if (inventoryItem is null)
            {
                inventoryItem = new Domain.Inventory.InventoryItem
                {
                    WarehouseId = order.WarehouseId,
                    ProductId = item.ProductId,
                    ProductVariantId = null,
                    QuantityOnHand = 0,
                    QuantityReserved = 0,
                    ReorderLevel = 0,
                    ReorderQuantity = 0,
                    AllowBackorder = false,
                    LastStockUpdateUtc = utcNow,
                };
                _dbContext.InventoryItems.Add(inventoryItem);
            }

            inventoryItem.QuantityOnHand += line.QuantityReceived;
            inventoryItem.LastStockUpdateUtc = utcNow;
            inventoryItem.StockStatus = InventoryService.ComputeStockStatus(inventoryItem);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.StockMovements.Add(new StockMovement
            {
                InventoryItemId = inventoryItem.Id,
                MovementType = StockMovementType.PurchaseReceipt,
                QuantityChange = line.QuantityReceived,
                QuantityOnHandAfter = inventoryItem.QuantityOnHand,
                QuantityReservedAfter = inventoryItem.QuantityReserved,
                ReferenceType = nameof(GoodsReceipt),
                ReferenceId = receipt.Id,
                Reason = isOverride ? $"Purchase order over-receipt: {request.OverrideReason}" : $"Purchase order receipt ({order.OrderNumber})",
                OccurredAtUtc = utcNow,
                CreatedByUserId = _currentUserService.UserId,
            });
        }

        var allReceived = order.Items.All(i => i.QuantityReceived >= i.QuantityOrdered);
        order.Status = allReceived ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        if (allReceived)
        {
            order.CompletedAtUtc = utcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success(await MapReceiptToDtoAsync(receipt.Id, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<GoodsReceiptDto>>> GetReceiptHistoryAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.PurchaseOrders.AnyAsync(p => p.Id == purchaseOrderId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<GoodsReceiptDto>>(Error.NotFound("purchase_order.not_found", "Purchase order not found."));
        }

        var receipts = await _dbContext.GoodsReceipts
            .Where(r => r.PurchaseOrderId == purchaseOrderId)
            .Include(r => r.Items).ThenInclude(i => i.PurchaseOrderItem)
            .OrderByDescending(r => r.ReceivedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<GoodsReceiptDto>>(receipts.Select(ToDto).ToList());
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(CancellationToken cancellationToken) =>
        _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private async Task<PurchaseOrderDto> MapToDtoAsync(int id, CancellationToken cancellationToken)
    {
        var order = await _dbContext.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.Items)
            .AsNoTracking()
            .FirstAsync(p => p.Id == id, cancellationToken);

        return new PurchaseOrderDto(
            order.Id, order.OrderNumber, order.SupplierId, order.Supplier.Name, order.WarehouseId, order.Warehouse.Name,
            order.Status.ToString(), order.ExpectedDeliveryDate, order.Notes,
            order.SubmittedAtUtc, order.ApprovedAtUtc, order.CompletedAtUtc, order.CancelledAtUtc,
            order.Items.Select(ToDto).ToList(), order.RowVersion);
    }

    private async Task<GoodsReceiptDto> MapReceiptToDtoAsync(int id, CancellationToken cancellationToken)
    {
        var receipt = await _dbContext.GoodsReceipts
            .Include(r => r.Items).ThenInclude(i => i.PurchaseOrderItem)
            .AsNoTracking()
            .FirstAsync(r => r.Id == id, cancellationToken);

        return ToDto(receipt);
    }

    private static PurchaseOrderItemDto ToDto(PurchaseOrderItem item) => new(
        item.Id, item.ProductId, item.ProductName, item.ProductSku, item.QuantityOrdered, item.QuantityReceived, item.UnitCost);

    private static GoodsReceiptDto ToDto(GoodsReceipt receipt) => new(
        receipt.Id, receipt.PurchaseOrderId, receipt.ReceivedAtUtc, receipt.ReceivedByUserId, receipt.Notes, receipt.OverrideReason,
        receipt.Items.Select(i => new GoodsReceiptItemDto(i.Id, i.PurchaseOrderItemId, i.PurchaseOrderItem.ProductName, i.QuantityReceived, i.IsOverride)).ToList());
}
