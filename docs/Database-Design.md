# Database Design

## Status

The first migration, `InitialIdentityAndSecurity` (Milestone 1), adds ASP.NET
Core Identity's tables plus `RefreshTokens`, `UserSessions`, and
`SecurityAuditEvents`. The second, `CatalogSchema` (Milestone 2), adds
`Categories`, `Brands`, `Products`, `ProductTags`/`ProductTagMappings`,
`ProductImages`, `ProductAttributes`/`ProductAttributeValues`,
`ProductVariants`/`ProductVariantAttributeValues`, and
`ProductSpecifications`. The third, `InventorySchema` (Milestone 3.1), adds
`Warehouses`, `InventoryItems`, `StockMovements`, `StockAdjustments`, and
`InventoryReservations`. The fourth, `SupplierSchema` (Milestone 3.2), adds
`Suppliers` and `SupplierProducts`. The fifth, `PurchaseOrderSchema`
(Milestone 3.3), adds `PurchaseOrders`, `PurchaseOrderItems`,
`GoodsReceipts`, and `GoodsReceiptItems`. The sixth, `HomePageBannerSchema`
(Milestone 4.1), adds `HomePageBanners`. The seventh, `ProductPerformanceIndexes`
(Milestone 4.3), adds no tables - only three indexes on `Products`
(`IsActive`+`IsPublished` composite, `SellingPrice`, `PublishedAtUtc`) to
match the storefront listing queries' actual filter/sort shapes - see
`Data-Dictionary.md` for full column detail. No customer-order tables exist
yet; those arrive starting Milestone 9. This document grows into a full
ER-level description of the schema as they're introduced.

`ApplicationDbContext` now extends `IdentityDbContext<ApplicationUser,
IdentityRole, string>` rather than plain `DbContext`, so Identity's tables
share this context's migration history instead of a separate one.

## Standards applied to every table (Section 4 of the brief)

- Databases: `ECommerceAppDb` (development), `ECommerceAppTestDb` (automated
  integration tests only - tests must never run against dev or production).
- Standard columns on recoverable/auditable entities: `Id` (int identity),
  `CreatedAtUtc`, `CreatedByUserId`, `UpdatedAtUtc`, `UpdatedByUserId`,
  `IsDeleted`, `RowVersion` (SQL Server `rowversion`/timestamp column for
  optimistic concurrency).
- All timestamps are stored in UTC.
- All currency amounts use `decimal(18,2)` - never a floating-point type.
- Soft deletion applies to recoverable data (products, categories, brands,
  users, etc.). Immutable financial transaction records (payments, refunds,
  ledger entries, audit logs) are never soft-deleted or hard-deleted.
- Foreign keys and commonly searched fields are indexed; unique indexes are
  added on SKU, normalized email, category slug, product slug, coupon code,
  and order number as those tables are introduced.
- Order items snapshot product, price, tax, discount, and shipping data at
  the time of purchase so historical orders remain accurate even if catalog
  data changes later.

## Primary key strategy

Entities use an `int` identity surrogate key (`BaseEntity.Id`) for efficient
clustered indexes and joins. Business-facing identifiers that are safe to
expose externally (slugs, SKUs, order numbers, coupon codes) are separate,
uniquely-indexed fields - raw database IDs are not exposed in URLs or API
responses for entities like orders.

## User identifier type

Audit fields (`CreatedByUserId`/`UpdatedByUserId`) are typed as `string?` to
match ASP.NET Core Identity's default user key type, without Domain having to
reference Identity itself.

## Connection strings

See `README.md` for local SQL Server setup (Windows Authentication by
default) and User Secrets configuration. Connection strings are never
committed to `appsettings.json`.

## Delete-behavior choices (Milestone 2)

SQL Server rejects a foreign key (`CASCADE` or `SET NULL` alike) if it would
create a second path by which deleting one row could cascade to the same
descendant table - this surfaced as a real migration failure during this
milestone (`ProductImages` reachable both directly from `Products` and via
`ProductVariants`). The resolution, applied consistently:

- Prefer `RESTRICT` for FKs where the parent conceptually "owns" a shared
  classification (Category/Brand -> Product) - the application must reassign
  or remove dependents first.
- Use `CASCADE` only for a single, unambiguous parent->child path (Product ->
  its own Images/Variants/Specifications/TagMappings).
- Use `NO ACTION` (not `SET NULL`, which SQL Server treats the same as
  `CASCADE` for this check) plus explicit application-code cleanup wherever a
  second path to the same table would otherwise exist
  (`ProductService.DeleteVariantAsync` nulls out `ProductImage.ProductVariantId`
  before removing the variant).

None of this matters for soft-deleted entities in practice - `Remove()` on an
`AuditableEntity` is converted to an `IsDeleted = true` update, never a real
`DELETE`, so these constraints only ever fire for the plain-`BaseEntity` join
tables (`ProductTagMappings`, `ProductVariantAttributeValues`), which are
genuinely removed when unlinked.

## RowVersion is now actually enforced

`RowVersion` existed on `AuditableEntity` since the Foundation milestone but
was never configured as a real concurrency token - it was just an inert
`byte[]` column SQL Server never updated and EF Core never checked, so
optimistic concurrency silently did nothing. Fixed in this milestone via
`modelBuilder.Entity(...).Property(nameof(IHasRowVersion.RowVersion))
.IsRowVersion()` in `ApplicationDbContext.OnModelCreating`, applied to every
entity implementing `IHasRowVersion`. Verified with a real concurrency-conflict
integration test (`ConcurrencyTests`) that loads the same row into two
`DbContext` instances, saves one, then asserts the second throws
`DbUpdateConcurrencyException`. Milestone 3.1 adds the same test for
`InventoryItem` (`InventoryConcurrencyTests`), since two staff members
adjusting the same item's stock concurrently is a realistic scenario, not
just a theoretical one.

## Purchasable-unit modeling (Milestone 3.1)

The Milestone 3 brief describes `InventoryItem` as "one per purchasable
variant," but Milestone 2's `Product`/`ProductVariant` model never requires a
product to have variants - a product with `BaseSKU` and no variants is fully
purchasable on its own (confirmed by re-reading `ProductService`: there is no
"at least one variant" validation anywhere, and the product edit UI treats
"no variants yet" as a normal state, not an error). Changing that model now
would be an unrequested change to already-completed, tested Milestone 2 code.

Resolution: `InventoryItem` carries both `ProductId` (always set) and
`ProductVariantId` (nullable). A null `ProductVariantId` means the item
tracks the *product* itself; a non-null value means it tracks one specific
variant. Two filtered unique indexes prevent double-tracking the same
purchasable unit in the same warehouse - see `InventoryItemConfiguration`
and the `InventoryItems` entry in `Data-Dictionary.md`.

## Immutable ledger tables (Milestone 3.1)

`StockMovements` and `StockAdjustments` are the first tables in the solution
that deliberately do **not** derive from `AuditableEntity`. Both are
insert-only audit records - correcting a mistake means recording a new,
opposite entry, never editing or soft-deleting an old one - so they carry
only their own natural timestamp/actor columns (`OccurredAtUtc`/
`CreatedByUserId` and `AdjustedAtUtc`/`AdjustedByUserId` respectively)
instead of the general `IsDeleted`/`UpdatedAtUtc`/`RowVersion` set, which
would be actively misleading on a table nothing ever updates.

## Supplier-product linking scope (Milestone 3.2)

`SupplierProducts` links a `Supplier` to a `Product`, not to a specific
`ProductVariant`. Neither the original brief nor the restructured one
specifies variant-level sourcing, and purchase orders (Milestone 3.3) are
expected to operate at the same granularity as `InventoryItem` - which is
itself product-or-variant, not exclusively variant. Linking at the product
level keeps sourcing/cost/lead-time data attached to the item a buyer
actually reorders, while still covering the "product with variants" case
(the cost/SKU on the link is a default sourcing reference, not a per-variant
override). This can be revisited if Milestone 3.3's purchase-order line
items turn out to need variant-level supplier pricing.

## Purchase order design decisions (Milestone 3.3)

Several judgment calls, made because the brief specifies behavior at a
level above these mechanics:

- **Order numbering**: `PurchaseOrder.OrderNumber` is generated as
  `PO-{Id:D6}` right after insert (two `SaveChangesAsync` calls - the row
  needs its identity value first). This is deliberately different from the
  "non-sequential, no raw DB ID exposure" requirement Milestone 9 sets for
  *customer-facing* order numbers: a PO is an internal admin document, never
  shown to or enumerable by an untrusted party, so a simple sequential
  scheme is fine and more useful for staff (sortable, predictable).
- **Cancellation window**: `CancelAsync` only accepts Draft/Submitted/Approved.
  Once any goods have been received (PartiallyReceived), the order can no
  longer be cancelled outright - stock has already changed hands. The brief
  doesn't specify this explicitly; it's the natural reading of "Cancelled"
  being a pre-fulfillment terminal state, consistent with how `DeleteAsync`
  patterns elsewhere refuse to act once real-world consequences exist.
- **Item picker scoped to `SupplierProducts`**: adding a line to a PO only
  offers products already linked to that supplier (via `ISupplierService
  .GetLinkedProductsAsync`), pre-filling `UnitCost` from the link's
  `CostPrice`. This ties M3.2 and M3.3 together as intended, rather than
  letting a PO reference a product the supplier was never linked to.
- **Auto-created `InventoryItem` on first receipt**: if no `InventoryItem`
  exists yet for (warehouse, product) when a `GoodsReceipt` line is applied,
  one is created on the fly (matching `PurchaseReceipt` already being a
  first-class `StockMovementType`) rather than requiring a separate manual
  "record opening stock" step first. If one already exists, it's reused and
  updated in place - confirmed during manual verification against a product
  with prior M3.1 opening-stock history.
- **Shared stock-status logic, separate transactions**: `PurchaseOrderService
  .ReceiveAsync` needs the exact same InStock/LowStock/OutOfStock/Backorder
  logic `InventoryService` already implements. Rather than injecting
  `IInventoryService` and calling into it mid-transaction (risking a nested
  `BeginTransactionAsync` on the same connection - EF Core does not support
  that), `InventoryService.ComputeStockStatus` was changed from `private` to
  `internal static` (pure function, no side effects) and is called directly.
  `PurchaseOrderService` owns and commits its own transaction end-to-end,
  writing `InventoryItem`/`StockMovement` rows itself.

## Explicit transactions and the EF Core InMemory provider

`InventoryService` wraps each multi-write operation (recording opening
stock, adjusting stock, reserving/releasing stock - each of which writes
both an `InventoryItem` change and a corresponding ledger row) in an
explicit `Database.BeginTransactionAsync()`, per the Milestone 3 brief's "use
transactions + row-version concurrency throughout." The EF Core InMemory
provider used by the fast unit-style `Infrastructure.Tests` suite does not
support real transactions and throws `InvalidOperationException` the moment
one is requested (discovered by running the new inventory tests, not by
inspection - worth remembering for any future service that opens explicit
transactions). Fixed by only opening a transaction when
`_dbContext.Database.IsRelational()` is true, which is always true against
real SQL Server (dev, integration tests, production) and always false
against the InMemory provider - the unit tests exercise the exact same code
path minus the transaction wrapper, which is safe here because InMemory has
no concurrent writers to protect against in the first place.
