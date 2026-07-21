# Data Dictionary

Tables added by the `InitialIdentityAndSecurity` migration (Milestone 1), the
`CatalogSchema` migration (Milestone 2), and the `InventorySchema` migration
(Milestone 3.1).
ASP.NET Core Identity's own tables (`AspNetUsers`, `AspNetRoles`,
`AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`,
`AspNetUserTokens`, `AspNetRoleClaims`) follow the framework's standard
schema plus the extra columns listed below; only the application-specific
tables are fully documented column-by-column.

## AspNetUsers (extended)

In addition to Identity's standard columns (`Id`, `UserName`,
`NormalizedUserName`, `Email`, `NormalizedEmail`, `EmailConfirmed`,
`PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `PhoneNumber`,
`PhoneNumberConfirmed`, `TwoFactorEnabled`, `LockoutEnd`, `LockoutEnabled`,
`AccessFailedCount`):

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| FirstName | nvarchar(100) | No | Profile field |
| LastName | nvarchar(100) | No | Profile field |
| IsActive | bit | No | Admin-controlled permanent disable, separate from temporary lockout |
| CreatedAtUtc | datetime2 | No | Account creation timestamp (UTC) |
| LastSuccessfulLoginAtUtc | datetime2 | Yes | Updated on every successful login |
| PasswordChangedAtUtc | datetime2 | Yes | Updated on registration, change, and reset |

A unique index on `NormalizedEmail` enforces one account per email
(`options.User.RequireUniqueEmail = true` plus an explicit unique index,
since Identity only indexes `NormalizedUserName` by default).

## RefreshTokens

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| UserId | nvarchar(450) | No | FK to AspNetUsers.Id (indexed) |
| TokenHash | nvarchar(256) | No | SHA-256 hash of the raw token; unique index. Raw value is never stored |
| ExpiresAtUtc | datetime2 | No | |
| CreatedAtUtc | datetime2 | No | |
| CreatedByIp | nvarchar(64) | Yes | |
| RevokedAtUtc | datetime2 | Yes | Null while active |
| RevokedByIp | nvarchar(64) | Yes | |
| ReplacedByTokenHash | nvarchar(max) | Yes | Set when rotated, forming the reuse-detection chain |
| ReasonRevoked | nvarchar(200) | Yes | `"rotated"`, `"logout"`, `"reuse_detected"`, `"revoke_all_sessions"`, `"password_changed"`, `"password_reset"` |

## UserSessions

Lightweight login audit trail - not consulted to authorize requests (see
`Security.md`).

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| UserId | nvarchar(450) | No | FK to AspNetUsers.Id (indexed) |
| LoginMethod | int (enum) | No | `CookieMvc` or `JwtApi` |
| LoginAtUtc | datetime2 | No | |
| IpAddress | nvarchar(64) | Yes | |
| UserAgent | nvarchar(512) | Yes | |
| LoggedOutAtUtc | datetime2 | Yes | |
| IsRevoked | bit | No | Set by `RevokeAllSessionsAsync` |

## SecurityAuditEvents

Immutable security audit log - never edited or soft-deleted; corrections are
new rows, not mutations.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| UserId | nvarchar(450) | Yes | Null when the event has no matching account (e.g. login attempt with an unknown email) |
| EventType | int (enum) | No | See `Domain.Security.SecurityEventType` |
| OccurredAtUtc | datetime2 | No | Indexed |
| Succeeded | bit | No | |
| IpAddress | nvarchar(64) | Yes | |
| UserAgent | nvarchar(512) | Yes | |
| CorrelationId | nvarchar(max) | Yes | Ties the event to the request's `X-Correlation-Id` |
| Details | nvarchar(1000) | Yes | Safe, non-sensitive summary - never a password or token |

## Categories

Self-referencing for unlimited nesting; `ParentCategoryId` FK is `ON DELETE
RESTRICT` (though soft-deleting a row never triggers a physical delete - see
"Soft delete and RowVersion" below). Deleting a category with subcategories
or assigned products is rejected at the application level (`CategoryService`).

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| Name | nvarchar(200) | No | |
| Slug | nvarchar(250) | No | Unique index |
| Description | nvarchar(2000) | Yes | |
| ParentCategoryId | int | Yes | Self-referencing FK, `RESTRICT` |
| DisplayOrder | int | No | |
| ImagePath | nvarchar(500) | Yes | Web-relative path (e.g. `/uploads/categories/...`) |
| IsActive | bit | No | |
| IsFeatured | bit | No | |

## Brands

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| Name | nvarchar(200) | No | |
| Slug | nvarchar(250) | No | Unique index |
| Description | nvarchar(2000) | Yes | |
| LogoPath | nvarchar(500) | Yes | Web-relative path |
| Website | nvarchar(300) | Yes | |
| IsActive | bit | No | |
| IsFeatured | bit | No | |

## Products

`TaxCategory` and `ReturnEligibility` are plain strings for now - the
structured tax-rate model (Milestone 7) and return-window model (Milestone
13) don't exist yet. `CategoryId` is required; `BrandId` is optional (a
judgment call - not every product needs a brand).

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| Name | nvarchar(300) | No | |
| Slug | nvarchar(350) | No | Unique index |
| ShortDescription | nvarchar(500) | Yes | |
| FullDescription | nvarchar(max) | Yes | |
| BrandId | int | Yes | FK to Brands, `RESTRICT` |
| CategoryId | int | No | FK to Categories, `RESTRICT` |
| BaseSKU | nvarchar(100) | No | Unique index |
| CostPrice | decimal(18,2) | No | |
| SellingPrice | decimal(18,2) | No | Must be > 0 |
| CompareAtPrice | decimal(18,2) | Yes | Must be > SellingPrice when set |
| TaxCategory | nvarchar(50) | No | Free-form for now |
| IsTaxable | bit | No | |
| IsActive | bit | No | Admin on/off switch; deactivating auto-unpublishes |
| IsFeatured | bit | No | |
| IsPublished | bit | No | Requires IsActive |
| PublishedAtUtc | datetime2 | Yes | |
| Weight / Length / Width / Height | decimal(18,3) | Yes | |
| WarrantyInformation | nvarchar(1000) | Yes | |
| ReturnEligibility | nvarchar(500) | Yes | Free-form for now |
| LowStockThreshold | int | Yes | |
| SearchKeywords | nvarchar(500) | Yes | |
| MetaTitle | nvarchar(200) | Yes | SEO - kept on Product, no separate ProductSeoMetadata table (see Architecture.md) |
| MetaDescription | nvarchar(500) | Yes | SEO |

## ProductTags / ProductTagMappings

`ProductTags`: Id, Name (nvarchar(100)), Slug (nvarchar(120), unique).
`ProductTagMappings`: plain join (ProductId, ProductTagId), unique composite
index, both FKs cascade - it's a pure link with no further children.

## ProductImages

A product-level image when `ProductVariantId` is null, or a variant-specific
image otherwise. The `ProductVariantId` FK is `NO ACTION` (not `SET NULL`) at
the DB level - SQL Server rejects `SET NULL` here as a second cascade path
alongside Product's own cascade to ProductImages, so `ProductService
.DeleteVariantAsync` detaches (nulls) affected images in application code
before removing the variant.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| ProductId | int | No | FK to Products, cascade |
| ProductVariantId | int | Yes | FK to ProductVariants, `NO ACTION` |
| Path | nvarchar(500) | No | Web-relative path, e.g. `/uploads/products/{guid}.png` |
| AltText | nvarchar(200) | Yes | |
| DisplayOrder | int | No | |
| IsPrimary | bit | No | Only one primary image per product (enforced in `ProductService`) |

## ProductAttributes / ProductAttributeValues

Global, reusable attribute definitions (e.g. "Color") and their values (e.g.
"Red"), shared across all products - not per-product. `ProductAttributes.Name`
is unique; `ProductAttributeValues` has a unique index on
(ProductAttributeId, Value).

## ProductVariants

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| ProductId | int | No | FK to Products, cascade |
| SKU | nvarchar(100) | No | Unique index (global) |
| Barcode | nvarchar(100) | Yes | |
| CostPrice / SellingPrice / CompareAtPrice | decimal(18,2) | Yes | Overrides the parent Product's value when set |
| Weight | decimal(18,3) | Yes | Override |
| IsActive | bit | No | |
| CombinationKey | nvarchar(200) | No | Sorted, comma-joined attribute-value IDs; unique per (ProductId, CombinationKey) - this is what makes "duplicate variant combination" a DB-enforced impossibility, not just an application check |

`ProductVariantAttributeValues`: plain join (ProductVariantId,
ProductAttributeValueId), unique composite index. The
ProductAttributeValueId FK is `RESTRICT`, not cascade - removing an attribute
value must not silently delete the variants built from it.

## ProductSpecifications

Free-form spec rows (Id, ProductId, Name (nvarchar(200)), Value
(nvarchar(1000)), DisplayOrder) shown on the product detail page. FK to
Products is cascade.

## Warehouses

`IsDefault` marks the single warehouse the app treats as the default target
when no warehouse is specified; the schema supports many warehouses even
though only one is seeded/used today (Milestone 3 brief).

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| Name | nvarchar(200) | No | |
| Code | nvarchar(50) | No | Unique index |
| AddressLine1 / AddressLine2 | nvarchar(200) | Yes | |
| City / Region / Country | nvarchar(100) | Yes | |
| PostalCode | nvarchar(20) | Yes | |
| IsDefault | bit | No | |
| IsActive | bit | No | |

## InventoryItems

Tracks stock for one purchasable unit in one warehouse. A purchasable unit is
either a specific `ProductVariant` (when the product has variants) or the
`Product` itself (`ProductVariantId` null) - Milestone 2 never requires a
product to have variants. Two filtered unique indexes make it impossible to
double-track the same unit in the same warehouse:
`(WarehouseId, ProductId) WHERE ProductVariantId IS NULL` and
`(WarehouseId, ProductVariantId) WHERE ProductVariantId IS NOT NULL`.
`QuantityAvailable` (= QuantityOnHand - QuantityReserved) is a computed,
non-persisted property - never stored, so it can't drift from its inputs.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| WarehouseId | int | No | FK to Warehouses, `RESTRICT` |
| ProductId | int | No | FK to Products, `RESTRICT` |
| ProductVariantId | int | Yes | FK to ProductVariants, `RESTRICT` |
| QuantityOnHand | int | No | |
| QuantityReserved | int | No | |
| ReorderLevel | int | No | At/below this (and > 0), status becomes LowStock |
| ReorderQuantity | int | No | Suggested restock quantity - not yet consumed until Milestone 3.3 (purchase orders) |
| AllowBackorder | bit | No | When true, reservations may exceed on-hand |
| StockStatus | int (enum) | No | `InStock`/`LowStock`/`OutOfStock`/`Backorder` - denormalized and indexed for fast admin filtering, recomputed on every quantity change |
| LastStockUpdateUtc | datetime2 | No | |

## StockMovements

An immutable ledger entry for every stock change - insert-only, never updated
or deleted, so it deliberately does **not** derive from `AuditableEntity`
(no `IsDeleted`, `UpdatedAtUtc`, or `RowVersion` columns exist on this table).
Corrections are new, opposite-sign rows, never edits to history.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| InventoryItemId | int | No | FK to InventoryItems, `RESTRICT`, indexed |
| MovementType | int (enum) | No | OpeningStock / PurchaseReceipt / SaleReservation / SaleCompletion / ReservationRelease / CustomerReturn / SupplierReturn / Damage / Loss / ManualAdjustment / Transfer |
| QuantityChange | int | No | Signed; meaning depends on MovementType (on-hand delta for stock-count changes, reserved delta for reservation/release) |
| QuantityOnHandAfter | int | No | Snapshot for audit readability without replaying history |
| QuantityReservedAfter | int | No | Snapshot |
| ReferenceType | nvarchar(100) | Yes | Free-form pointer to the causing record (e.g. `"StockAdjustment"`, `"InventoryReservation"`) - not an FK, since several of those record types don't exist as entities until later milestones |
| ReferenceId | int | Yes | |
| Reason | nvarchar(500) | Yes | |
| OccurredAtUtc | datetime2 | No | Indexed |
| CreatedByUserId | nvarchar(450) | Yes | |

## StockAdjustments

A detailed, immutable record of a manual stock adjustment (who/why), in
addition to the generic StockMovements ledger row it produces. Also does not
derive from `AuditableEntity`, for the same immutability reason as
StockMovements. "With approval where configured" (Milestone 3 brief) has no
configuration source yet - Store Configuration lands in Milestone 16 - so
adjustments apply immediately for any `CanManageInventory`-authorized user.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| InventoryItemId | int | No | FK to InventoryItems, `RESTRICT`, indexed |
| QuantityDelta | int | No | Signed; on-hand adjustment (rejected if it would take on-hand below zero) |
| Reason | nvarchar(500) | No | Required |
| QuantityOnHandAfter | int | No | |
| AdjustedAtUtc | datetime2 | No | |
| AdjustedByUserId | nvarchar(450) | Yes | |

## InventoryReservations

Reserves stock against an `InventoryItem` for a cart or order - those callers
arrive in Milestones 6 and 9, so `ReferenceType`/`ReferenceId` are free-form
for now. Unlike the ledger tables above, this has a real lifecycle
(Active -> Released/Consumed/Expired), so it does use `AuditableEntity`.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| InventoryItemId | int | No | FK to InventoryItems, `RESTRICT`, indexed |
| Quantity | int | No | |
| Status | int (enum) | No | Active / Released / Consumed / Expired, indexed |
| ReferenceType | nvarchar(100) | Yes | |
| ReferenceId | nvarchar(100) | Yes | |
| ExpiresAtUtc | datetime2 | Yes | |
| ReleasedAtUtc | datetime2 | Yes | |

## Suppliers

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| Name | nvarchar(200) | No | |
| Code | nvarchar(50) | No | Unique index; used on purchase-order documents (Milestone 3.3) |
| ContactName | nvarchar(200) | Yes | |
| Email | nvarchar(256) | Yes | |
| Phone | nvarchar(50) | Yes | |
| AddressLine1 / AddressLine2 | nvarchar(200) | Yes | |
| City / Region / Country | nvarchar(100) | Yes | |
| PostalCode | nvarchar(20) | Yes | |
| Website | nvarchar(500) | Yes | |
| Notes | nvarchar(2000) | Yes | |
| IsActive | bit | No | |

## SupplierProducts

Plain join record linking a `Supplier` to a `Product` it can source -
mirrors `ProductTagMappings`: derives from `BaseEntity`, not
`AuditableEntity`, so unlinking removes the row outright rather than
soft-deleting it (a link is either present or not; there is no meaningful
"deleted link" state to recover from a recycle bin).

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| SupplierId | int | No | FK to Suppliers, cascade; unique composite index with ProductId |
| ProductId | int | No | FK to Products, cascade |
| SupplierSku | nvarchar(100) | Yes | The supplier's own part number/SKU for this product |
| CostPrice | decimal(18,2) | Yes | Negotiated cost from this supplier; falls back to the product's own CostPrice in the UI when unset |
| LeadTimeDays | int | Yes | |
| IsPreferred | bit | No | Marks this as the preferred supplier for the product (informational only - no enforcement that only one link per product is preferred) |

## PurchaseOrders

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| SupplierId | int | No | FK to Suppliers, `RESTRICT` |
| WarehouseId | int | No | FK to Warehouses, `RESTRICT` - the receiving warehouse |
| OrderNumber | nvarchar(20) | No | Unique index; generated as `PO-{Id:D6}` after insert (internal document number, not customer-facing - unlike the non-sequential order numbers Milestone 9 requires for customer orders, sequential is fine here) |
| Status | int (enum) | No | Draft / Submitted / Approved / PartiallyReceived / Received / Cancelled, indexed |
| ExpectedDeliveryDate | datetime2 | Yes | |
| Notes | nvarchar(2000) | Yes | |
| SubmittedAtUtc / ApprovedAtUtc / CompletedAtUtc / CancelledAtUtc | datetime2 | Yes | Lifecycle timestamps |
| ApprovedByUserId / CancelledByUserId | nvarchar(450) | Yes | |

## PurchaseOrderItems

Product-level, not variant-level - matches `SupplierProducts`' granularity
(see the "Supplier-product linking scope" note in `Database-Design.md`).
`ProductName`/`ProductSku` are snapshotted at add-time so a PO's history
stays accurate even if the product is later renamed or re-SKU'd.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| PurchaseOrderId | int | No | FK to PurchaseOrders, cascade |
| ProductId | int | No | FK to Products, `RESTRICT` |
| ProductName | nvarchar(200) | No | Snapshot |
| ProductSku | nvarchar(100) | No | Snapshot |
| QuantityOrdered | int | No | |
| QuantityReceived | int | No | Running total across all goods receipts for this line |
| UnitCost | decimal(18,2) | No | Snapshot of cost at order time |

## GoodsReceipts

An immutable receiving event against a `PurchaseOrder` - insert-only, like
`StockMovements`/`StockAdjustments`, so it deliberately does not derive from
`AuditableEntity` (no `IsDeleted`, `UpdatedAtUtc`, or `RowVersion` columns).

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| PurchaseOrderId | int | No | FK to PurchaseOrders, `RESTRICT`, indexed |
| ReceivedAtUtc | datetime2 | No | Indexed |
| ReceivedByUserId | nvarchar(450) | Yes | |
| Notes | nvarchar(2000) | Yes | |
| OverrideReason | nvarchar(500) | Yes | Set only when at least one line in this receipt exceeded its outstanding quantity |

## GoodsReceiptItems

Immutable line of a `GoodsReceipt` - same non-`AuditableEntity` reasoning.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| GoodsReceiptId | int | No | FK to GoodsReceipts, cascade, indexed |
| PurchaseOrderItemId | int | No | FK to PurchaseOrderItems, `RESTRICT`, indexed |
| QuantityReceived | int | No | |
| IsOverride | bit | No | True if this specific line received more than was outstanding |

## HomePageBanners

Admin-managed home page content (Milestone 4.1 brief requires hero banners
and promo blocks to be admin-managed, not hardcoded). `ImagePath` is
nullable - a banner can be created before its image is uploaded (same
two-step create-then-upload flow as `Brand.LogoPath`) - and the storefront
home page excludes any banner without one.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| Title | nvarchar(200) | No | |
| Subtitle | nvarchar(500) | Yes | |
| ImagePath | nvarchar(500) | Yes | Null until an image is uploaded; storefront excludes imageless banners |
| LinkUrl | nvarchar(500) | Yes | Admin-supplied, rendered as a real link (unlike system-generated category/product links, which stay non-clickable until their destination pages exist) |
| BannerType | int (enum) | No | Hero / Promo, indexed with DisplayOrder |
| DisplayOrder | int | No | |
| IsActive | bit | No | |

## RecentlyViewedItems

Authenticated-customer recently-viewed history (Milestone 5.3). A guest's
history is tracked in a cookie instead - a comma-separated list of product
IDs on a single `HttpOnly` cookie, no table involved - so this table only
ever holds rows for signed-in users. One row per `(UserId, ProductId)`;
viewing the same product again updates `ViewedAtUtc` in place rather than
inserting a duplicate, and rows beyond `Store:RecentlyViewedMaxItems` (config,
default 10) are deleted on every view, oldest `ViewedAtUtc` first.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| UserId | nvarchar(450) | No | Plain string, not a navigation property - `ApplicationUser` lives in Infrastructure and Domain cannot reference it (same pattern as `RefreshTokens`/`UserSessions`) |
| ProductId | int (FK -> Products) | No | Cascade delete |
| ViewedAtUtc | datetime2 | No | Updated (not inserted) on a repeat view; drives both display order and trim-to-max |

Indexes: unique on `(UserId, ProductId)`; non-unique on `(UserId, ViewedAtUtc)`
for the ordered-history query. Plain `BaseEntity` - no soft delete or
`RowVersion`, same reasoning as `ProductTagMappings`/`SupplierProducts`: a
rolling, self-pruning record with no audit or concurrency need.

## Carts / CartItems

Cart core (Milestone 6.1) plus merge and pricing/stock integrity (Milestone
6.2). A `Cart` is owned by exactly one of `UserId` (authenticated) or
`GuestToken` (a value from a cookie, never both/neither - two filtered
unique indexes enforce this) and is created lazily on the first item added.
`LineTotal` is always computed live via `IPricingService`, never from a
stored value - `CartItem.PriceWhenAdded` exists only so a read can *detect
and flag* that the price changed, never to charge it.

**Carts**

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| UserId | nvarchar(450) | Yes | Set for an authenticated user's cart; unique when present |
| GuestToken | nvarchar(64) | Yes | Set for a guest's cart (from the `CartGuestToken` cookie); unique when present |
| CreatedAtUtc | datetime2 | No | |
| UpdatedAtUtc | datetime2 | No | Bumped on every add/update/remove/clear |

**CartItems**

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| CartId | int (FK -> Carts) | No | Cascade delete |
| ProductId | int (FK -> Products) | No | Restrict - same reasoning as `InventoryItems` (Milestone 3.1): a product is never physically deleted, and a `Cascade` here plus an indirect path via `ProductVariant -> Product` would be a multiple-cascade-paths error in SQL Server |
| ProductVariantId | int (FK -> ProductVariants) | Yes | Restrict, same reasoning; null for a product with no variants |
| Quantity | int | No | Never silently changed by a read - a stock shortfall is only ever flagged (`QuantityExceedsStock`), and the customer adjusts it themselves |
| PriceWhenAdded | decimal(18,2) | No | Milestone 6.2. Re-stamped to the current live price on every add/merge/quantity-update - never read for billing, only compared against the live price to flag `PriceChanged` |
| AddedAtUtc | datetime2 | No | |

Indexes: unique on `(CartId, ProductId)` filtered where `ProductVariantId IS
NULL`, and unique on `(CartId, ProductVariantId)` filtered where it `IS NOT
NULL` - the same "one purchasable unit, simple-or-variant, never both" pair
of filtered indexes `InventoryItems` uses. Both tables are plain
`BaseEntity` - no soft delete or `RowVersion`, the same reasoning as
`ProductTagMappings`/`RecentlyViewedItems`: rolling, low-stakes records with
no audit or concurrency need.

## WishlistItems

Wishlist (Milestone 6.3) - account-only, no guest cookie support (unlike
Cart), and product-level only, no variant (a lighter bookmark than a cart
line - variant selection happens later if the customer moves it into their
cart).

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| UserId | nvarchar(450) | No | Plain string, not a navigation property - same reasoning as `RecentlyViewedItems`/`RefreshTokens` |
| ProductId | int (FK -> Products) | No | Cascade delete - safe here (unlike `CartItems`) since there's no second FK to `ProductVariants` creating a multi-cascade-path conflict |
| AddedAtUtc | datetime2 | No | Drives display order (most-recently-added first) |

Indexes: unique on `(UserId, ProductId)` - toggling an already-wishlisted
product removes the row rather than allowing a duplicate. Plain
`BaseEntity` - no soft delete or `RowVersion`, same reasoning as
`RecentlyViewedItems`.

## Soft delete and RowVersion

Every table above (except the pure join tables `ProductTagMappings`,
`ProductVariantAttributeValues`, `SupplierProducts`, `RecentlyViewedItems`,
`Carts`, `CartItems`, and `WishlistItems`; and the immutable ledger tables
`StockMovements`, `StockAdjustments`, `GoodsReceipts`, and `GoodsReceiptItems`) inherits `AuditableEntity`:
`IsDeleted` (soft
delete, globally filtered out of normal queries) and `RowVersion` - a real SQL
Server `rowversion`/`timestamp` column and EF Core concurrency token (see
`ApplicationDbContext.OnModelCreating`'s `IsRowVersion()` configuration).
This was fixed during Milestone 2's own testing: `RowVersion` had existed
since Milestone 1 but was never actually wired up as a concurrency token, so
SQL Server never generated it and EF Core never checked it - optimistic
concurrency was silently a no-op until this was caught.
