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

public sealed class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUserService;

    public InventoryService(ApplicationDbContext dbContext, IClock clock, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserService = currentUserService;
    }

    public async Task<Result<WarehouseDto>> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.Warehouses.AnyAsync(w => w.Code == request.Code, cancellationToken))
        {
            return Result.Failure<WarehouseDto>(Error.Conflict("warehouse.duplicate_code", $"A warehouse with the code '{request.Code}' already exists."));
        }

        if (request.IsDefault)
        {
            await ClearExistingDefaultAsync(cancellationToken);
        }

        var warehouse = new Warehouse
        {
            Name = request.Name,
            Code = request.Code,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            Region = request.Region,
            PostalCode = request.PostalCode,
            Country = request.Country,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
        };

        _dbContext.Warehouses.Add(warehouse);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(warehouse));
    }

    public async Task<Result<WarehouseDto>> UpdateWarehouseAsync(UpdateWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        var warehouse = await _dbContext.Warehouses.FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);
        if (warehouse is null)
        {
            return Result.Failure<WarehouseDto>(Error.NotFound("warehouse.not_found", "Warehouse not found."));
        }

        if (await _dbContext.Warehouses.AnyAsync(w => w.Code == request.Code && w.Id != request.Id, cancellationToken))
        {
            return Result.Failure<WarehouseDto>(Error.Conflict("warehouse.duplicate_code", $"A warehouse with the code '{request.Code}' already exists."));
        }

        if (request.IsDefault && !warehouse.IsDefault)
        {
            await ClearExistingDefaultAsync(cancellationToken);
        }

        warehouse.Name = request.Name;
        warehouse.Code = request.Code;
        warehouse.AddressLine1 = request.AddressLine1;
        warehouse.AddressLine2 = request.AddressLine2;
        warehouse.City = request.City;
        warehouse.Region = request.Region;
        warehouse.PostalCode = request.PostalCode;
        warehouse.Country = request.Country;
        warehouse.IsDefault = request.IsDefault;
        warehouse.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(warehouse));
    }

    public async Task<Result<WarehouseDto>> GetWarehouseByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var warehouse = await _dbContext.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        return warehouse is null
            ? Result.Failure<WarehouseDto>(Error.NotFound("warehouse.not_found", "Warehouse not found."))
            : Result.Success(ToDto(warehouse));
    }

    public async Task<Result<IReadOnlyList<WarehouseDto>>> GetWarehousesAsync(bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Warehouses.AsNoTracking().AsQueryable();
        if (onlyActive)
        {
            query = query.Where(w => w.IsActive);
        }

        var warehouses = await query.OrderBy(w => w.Name).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<WarehouseDto>>(warehouses.Select(ToDto).ToList());
    }

    public async Task<Result> DeactivateWarehouseAsync(int id, CancellationToken cancellationToken = default) => await SetWarehouseActiveAsync(id, false, cancellationToken);

    public async Task<Result> ActivateWarehouseAsync(int id, CancellationToken cancellationToken = default) => await SetWarehouseActiveAsync(id, true, cancellationToken);

    public async Task<Result<InventoryItemDto>> RecordOpeningStockAsync(RecordOpeningStockRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken))
        {
            return Result.Failure<InventoryItemDto>(Error.NotFound("inventory.warehouse_not_found", "Warehouse not found."));
        }

        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<InventoryItemDto>(Error.NotFound("inventory.product_not_found", "Product not found."));
        }

        if (request.ProductVariantId.HasValue)
        {
            var variantBelongsToProduct = await _dbContext.ProductVariants
                .AnyAsync(v => v.Id == request.ProductVariantId.Value && v.ProductId == request.ProductId, cancellationToken);
            if (!variantBelongsToProduct)
            {
                return Result.Failure<InventoryItemDto>(Error.Validation("inventory.variant_mismatch", "The selected variant does not belong to the selected product."));
            }
        }

        var alreadyTracked = request.ProductVariantId.HasValue
            ? await _dbContext.InventoryItems.AnyAsync(i => i.WarehouseId == request.WarehouseId && i.ProductVariantId == request.ProductVariantId, cancellationToken)
            : await _dbContext.InventoryItems.AnyAsync(i => i.WarehouseId == request.WarehouseId && i.ProductId == request.ProductId && i.ProductVariantId == null, cancellationToken);

        if (alreadyTracked)
        {
            return Result.Failure<InventoryItemDto>(Error.Conflict(
                "inventory.already_tracked",
                "This product is already tracked in this warehouse. Use a stock adjustment to change its quantity instead."));
        }

        var utcNow = _clock.UtcNow;
        var item = new InventoryItem
        {
            WarehouseId = request.WarehouseId,
            ProductId = request.ProductId,
            ProductVariantId = request.ProductVariantId,
            QuantityOnHand = request.Quantity,
            QuantityReserved = 0,
            ReorderLevel = request.ReorderLevel,
            ReorderQuantity = request.ReorderQuantity,
            AllowBackorder = request.AllowBackorder,
            LastStockUpdateUtc = utcNow,
        };
        item.StockStatus = ComputeStockStatus(item);

        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);

        _dbContext.InventoryItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        AddMovement(item, StockMovementType.OpeningStock, request.Quantity, "Opening stock", null, null, utcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success(await MapToDtoAsync(item.Id, cancellationToken));
    }

    public async Task<Result<InventoryItemDto>> GetInventoryItemByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.InventoryItems
            .Include(i => i.Warehouse)
            .Include(i => i.Product)
            .Include(i => i.ProductVariant)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        return item is null
            ? Result.Failure<InventoryItemDto>(Error.NotFound("inventory.item_not_found", "Inventory item not found."))
            : Result.Success(ToDto(item));
    }

    public async Task<Result<PagedResult<InventoryItemDto>>> GetOverviewAsync(InventoryItemQuery query, CancellationToken cancellationToken = default)
    {
        var items = _dbContext.InventoryItems
            .Include(i => i.Warehouse)
            .Include(i => i.Product)
            .Include(i => i.ProductVariant)
            .AsQueryable();

        if (query.WarehouseId.HasValue)
        {
            items = items.Where(i => i.WarehouseId == query.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            items = items.Where(i =>
                i.Product.Name.Contains(query.Search) ||
                i.Product.BaseSKU.Contains(query.Search) ||
                (i.ProductVariant != null && i.ProductVariant.SKU.Contains(query.Search)));
        }

        if (query.OnlyLowStock)
        {
            items = items.Where(i => i.StockStatus == StockStatus.LowStock);
        }

        if (query.OnlyOutOfStock)
        {
            items = items.Where(i => i.StockStatus == StockStatus.OutOfStock || i.StockStatus == StockStatus.Backorder);
        }

        items = items.OrderBy(i => i.Product.Name);

        var totalCount = await items.CountAsync(cancellationToken);
        var page = await items
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<InventoryItemDto>(page.Select(ToDto).ToList(), totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<InventoryItemDto>> AdjustStockAsync(AdjustStockRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken);
        if (item is null)
        {
            return Result.Failure<InventoryItemDto>(Error.NotFound("inventory.item_not_found", "Inventory item not found."));
        }

        var newOnHand = item.QuantityOnHand + request.QuantityDelta;
        if (newOnHand < 0)
        {
            return Result.Failure<InventoryItemDto>(Error.Validation("inventory.negative_stock", "This adjustment would reduce on-hand quantity below zero."));
        }

        var utcNow = _clock.UtcNow;
        item.QuantityOnHand = newOnHand;
        item.LastStockUpdateUtc = utcNow;
        item.StockStatus = ComputeStockStatus(item);

        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);

        var adjustment = new StockAdjustment
        {
            InventoryItemId = item.Id,
            QuantityDelta = request.QuantityDelta,
            Reason = request.Reason,
            QuantityOnHandAfter = newOnHand,
            AdjustedAtUtc = utcNow,
            AdjustedByUserId = _currentUserService.UserId,
        };
        _dbContext.StockAdjustments.Add(adjustment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        AddMovement(item, StockMovementType.ManualAdjustment, request.QuantityDelta, request.Reason, nameof(StockAdjustment), adjustment.Id, utcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success(await MapToDtoAsync(item.Id, cancellationToken));
    }

    public async Task<Result<InventoryReservationDto>> ReserveStockAsync(ReserveStockRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken);
        if (item is null)
        {
            return Result.Failure<InventoryReservationDto>(Error.NotFound("inventory.item_not_found", "Inventory item not found."));
        }

        if (request.Quantity > item.QuantityAvailable && !item.AllowBackorder)
        {
            return Result.Failure<InventoryReservationDto>(Error.Validation(
                "inventory.insufficient_stock",
                "Not enough stock available to reserve this quantity."));
        }

        var utcNow = _clock.UtcNow;
        item.QuantityReserved += request.Quantity;
        item.LastStockUpdateUtc = utcNow;
        item.StockStatus = ComputeStockStatus(item);

        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);

        var reservation = new InventoryReservation
        {
            InventoryItemId = item.Id,
            Quantity = request.Quantity,
            Status = ReservationStatus.Active,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
        };
        _dbContext.InventoryReservations.Add(reservation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        AddMovement(item, StockMovementType.SaleReservation, request.Quantity, "Stock reserved", nameof(InventoryReservation), reservation.Id, utcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success(ToDto(reservation));
    }

    public async Task<Result> ReleaseReservationAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.InventoryReservations.FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        if (reservation is null)
        {
            return Result.Failure(Error.NotFound("inventory.reservation_not_found", "Reservation not found."));
        }

        if (reservation.Status != ReservationStatus.Active)
        {
            return Result.Failure(Error.Validation("inventory.reservation_not_active", "Only an active reservation can be released."));
        }

        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(i => i.Id == reservation.InventoryItemId, cancellationToken);
        if (item is null)
        {
            return Result.Failure(Error.NotFound("inventory.item_not_found", "Inventory item not found."));
        }

        var utcNow = _clock.UtcNow;
        item.QuantityReserved -= reservation.Quantity;
        item.LastStockUpdateUtc = utcNow;
        item.StockStatus = ComputeStockStatus(item);

        reservation.Status = ReservationStatus.Released;
        reservation.ReleasedAtUtc = utcNow;

        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        AddMovement(item, StockMovementType.ReservationRelease, -reservation.Quantity, "Reservation released", nameof(InventoryReservation), reservation.Id, utcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> ConsumeReservationAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.InventoryReservations.FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        if (reservation is null)
        {
            return Result.Failure(Error.NotFound("inventory.reservation_not_found", "Reservation not found."));
        }

        if (reservation.Status != ReservationStatus.Active)
        {
            return Result.Failure(Error.Validation("inventory.reservation_not_active", "Only an active reservation can be consumed."));
        }

        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(i => i.Id == reservation.InventoryItemId, cancellationToken);
        if (item is null)
        {
            return Result.Failure(Error.NotFound("inventory.item_not_found", "Inventory item not found."));
        }

        var utcNow = _clock.UtcNow;
        item.QuantityOnHand -= reservation.Quantity;
        item.QuantityReserved -= reservation.Quantity;
        item.LastStockUpdateUtc = utcNow;
        item.StockStatus = ComputeStockStatus(item);

        reservation.Status = ReservationStatus.Consumed;

        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        AddMovement(item, StockMovementType.SaleCompletion, -reservation.Quantity, "Shipped", nameof(InventoryReservation), reservation.Id, utcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<PagedResult<StockMovementDto>>> GetMovementHistoryAsync(int inventoryItemId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.InventoryItems.AnyAsync(i => i.Id == inventoryItemId, cancellationToken))
        {
            return Result.Failure<PagedResult<StockMovementDto>>(Error.NotFound("inventory.item_not_found", "Inventory item not found."));
        }

        var movements = _dbContext.StockMovements
            .Where(m => m.InventoryItemId == inventoryItemId)
            .OrderByDescending(m => m.OccurredAtUtc).ThenByDescending(m => m.Id);

        var totalCount = await movements.CountAsync(cancellationToken);
        var page = await movements
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<StockMovementDto>(page.Select(ToDto).ToList(), totalCount, query.Page, query.PageSize));
    }

    private async Task<Result> SetWarehouseActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var warehouse = await _dbContext.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (warehouse is null)
        {
            return Result.Failure(Error.NotFound("warehouse.not_found", "Warehouse not found."));
        }

        warehouse.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// The EF Core InMemory provider (used by the fast unit-style Infrastructure.Tests suite)
    /// does not support real transactions and throws if one is requested, so this only opens
    /// one against a real relational provider (SQL Server in dev/prod/integration tests).
    /// </summary>
    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(CancellationToken cancellationToken) =>
        _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private async Task ClearExistingDefaultAsync(CancellationToken cancellationToken)
    {
        var currentDefaults = await _dbContext.Warehouses.Where(w => w.IsDefault).ToListAsync(cancellationToken);
        foreach (var warehouse in currentDefaults)
        {
            warehouse.IsDefault = false;
        }
    }

    /// <summary>
    /// InStock/LowStock are relative to ReorderLevel; OutOfStock vs. Backorder is
    /// decided by whether the item allows selling past zero available quantity.
    /// Internal (not private) so PurchaseOrderService can reuse this pure calculation
    /// when receiving goods, without coupling to InventoryService's own transaction
    /// boundary (each service owns and commits its own transaction independently).
    /// </summary>
    internal static StockStatus ComputeStockStatus(InventoryItem item)
    {
        var available = item.QuantityOnHand - item.QuantityReserved;
        if (available > item.ReorderLevel)
        {
            return StockStatus.InStock;
        }

        if (available > 0)
        {
            return StockStatus.LowStock;
        }

        return item.AllowBackorder ? StockStatus.Backorder : StockStatus.OutOfStock;
    }

    private void AddMovement(
        InventoryItem item,
        StockMovementType type,
        int quantityChange,
        string? reason,
        string? referenceType,
        int? referenceId,
        DateTime utcNow)
    {
        _dbContext.StockMovements.Add(new StockMovement
        {
            InventoryItemId = item.Id,
            MovementType = type,
            QuantityChange = quantityChange,
            QuantityOnHandAfter = item.QuantityOnHand,
            QuantityReservedAfter = item.QuantityReserved,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Reason = reason,
            OccurredAtUtc = utcNow,
            CreatedByUserId = _currentUserService.UserId,
        });
    }

    private async Task<InventoryItemDto> MapToDtoAsync(int inventoryItemId, CancellationToken cancellationToken)
    {
        var loaded = await _dbContext.InventoryItems
            .Include(i => i.Warehouse)
            .Include(i => i.Product)
            .Include(i => i.ProductVariant)
            .AsNoTracking()
            .FirstAsync(i => i.Id == inventoryItemId, cancellationToken);
        return ToDto(loaded);
    }

    private static InventoryItemDto ToDto(InventoryItem item) => new(
        item.Id,
        item.WarehouseId,
        item.Warehouse.Name,
        item.ProductId,
        item.Product.Name,
        item.ProductVariantId,
        item.ProductVariant?.SKU ?? item.Product.BaseSKU,
        item.QuantityOnHand,
        item.QuantityReserved,
        item.QuantityAvailable,
        item.ReorderLevel,
        item.ReorderQuantity,
        item.AllowBackorder,
        item.StockStatus.ToString(),
        item.LastStockUpdateUtc,
        item.RowVersion);

    private static WarehouseDto ToDto(Warehouse warehouse) => new(
        warehouse.Id,
        warehouse.Name,
        warehouse.Code,
        warehouse.AddressLine1,
        warehouse.AddressLine2,
        warehouse.City,
        warehouse.Region,
        warehouse.PostalCode,
        warehouse.Country,
        warehouse.IsDefault,
        warehouse.IsActive);

    private static InventoryReservationDto ToDto(InventoryReservation reservation) => new(
        reservation.Id,
        reservation.InventoryItemId,
        reservation.Quantity,
        reservation.Status.ToString(),
        reservation.ReferenceType,
        reservation.ReferenceId,
        reservation.ExpiresAtUtc,
        reservation.ReleasedAtUtc);

    private static StockMovementDto ToDto(StockMovement movement) => new(
        movement.Id,
        movement.InventoryItemId,
        movement.MovementType.ToString(),
        movement.QuantityChange,
        movement.QuantityOnHandAfter,
        movement.QuantityReservedAfter,
        movement.ReferenceType,
        movement.ReferenceId,
        movement.Reason,
        movement.OccurredAtUtc,
        movement.CreatedByUserId);
}
