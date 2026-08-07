# Data Dictionary

Complete, final schema (M1 through M18.1) - every application-specific table
the app has, in the order its migration introduced it: Identity's own tables
plus `RefreshTokens`/`UserSessions`/`SecurityAuditEvents` (M1), catalog
(M2), inventory/suppliers/purchase orders (M3), `HomePageBanners` (M4.1),
`RecentlyViewedItems` (M5.3), `Carts`/`CartItems` (M6), `WishlistItems`
(M6.3), `Promotions` (M7.1), `TaxRates` (M7.2), `ShippingMethods` (M7.3),
`Addresses` (M8.1), `Orders`/`OrderItems` (M9.1), `Payments` (M9.2),
`Shipments` (M10.3), `Reviews` (M12.1), `ReviewReports` (M12.2),
`ReturnRequests`/`ReturnRequestItems` (M13.2), `Refunds` (M13.3),
`OutboxMessages` (M15.2), and `StoreSettings` (M16.3). Admin user management
(M16.1) and audit logging (M16.2) introduced no new tables - both read/write
`AspNetUsers` and `SecurityAuditEvents`, which already existed since M1.
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
| AppliedPromotionId | int (FK -> Promotions) | Yes | Restrict. Milestone 7.1 - the cart's one currently-applied coupon, re-validated (not just re-read) on every `BuildCartDtoAsync` call and silently cleared if it's become invalid |

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

## Promotions

Promotions & coupons (Milestone 7.1) - either automatic (`CouponCode` null,
not yet auto-applied to any cart - see `Architecture.md`) or code-based
(customer enters `CouponCode` on the Cart page). Only one scope FK is ever
set, matching `ScopeType`.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| Name | nvarchar(200) | No | Admin-facing label |
| Description | nvarchar(500) | Yes | |
| CouponCode | nvarchar(50) | Yes | Unique when present (filtered index); null means automatic |
| DiscountType | int (enum: Percentage/FixedAmount) | No | |
| DiscountValue | decimal(18,2) | No | A percentage (0-100) or a currency amount, per `DiscountType` |
| ScopeType | int (enum: EntireOrder/Category/Brand/Product) | No | Determines which of the three scope FKs (if any) is set |
| ScopeCategoryId | int (FK -> Categories) | Yes | Restrict; set only when `ScopeType = Category` |
| ScopeBrandId | int (FK -> Brands) | Yes | Restrict; set only when `ScopeType = Brand` |
| ScopeProductId | int (FK -> Products) | Yes | Restrict; set only when `ScopeType = Product` |
| MinimumOrderAmount | decimal(18,2) | Yes | Checked against the cart's full subtotal, regardless of scope |
| MaxDiscountAmount | decimal(18,2) | Yes | Caps a `Percentage` discount's currency amount |
| StartsAtUtc | datetime2 | No | |
| EndsAtUtc | datetime2 | Yes | Null means no end date |
| MaxTotalUses | int | Yes | Recorded, **not enforced** - see `Architecture.md`'s Milestone 7.1 section |
| MaxUsesPerCustomer | int | Yes | Recorded, **not enforced** - same reason |
| IsActive | bool | No | |

Indexes: unique on `CouponCode` filtered where `IS NOT NULL`. `Auditable
Entity` (soft delete + `RowVersion`), same as `HomePageBanners` - an
admin-managed content table, not a rolling/ledger one.

## TaxRates

Tax service (Milestone 7.2) - maps a jurisdiction + product tax category to
a percentage. `TaxCategory` is matched against `Products.TaxCategory` by
plain case-insensitive string equality, not a shared FK/enum - see
`Architecture.md`'s Milestone 7.2 section for why. There's no real customer
destination to calculate against until Milestone 8.1's Addresses exist, so
this table is consumed today only as an estimate against the store's
configured default jurisdiction (`Store:DefaultTaxCountryCode`/
`Store:DefaultTaxRegionCode`).

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| CountryCode | nvarchar(2) | No | ISO 3166-1 alpha-2 (e.g. `US`, `PK`) |
| RegionCode | nvarchar(10) | Yes | Sub-national (e.g. a US state); null means a whole-country rate |
| TaxCategory | nvarchar(50) | No | Matched against `Products.TaxCategory`, case-insensitive |
| RatePercent | decimal(9,4) | No | 0-100; extra precision vs. the usual decimal(18,2) money convention since real combined rates carry fractional precision (e.g. 7.375%) |
| IsActive | bool | No | |

Indexes: unique on `(CountryCode, TaxCategory)` filtered where `RegionCode
IS NULL` (one whole-country rate per category), and unique on
`(CountryCode, RegionCode, TaxCategory)` filtered where `RegionCode IS NOT
NULL` (one region-specific rate per category) - the same dual-filtered-
index technique `Carts`' `UserId`/`GuestToken` pair uses. `AuditableEntity`
(soft delete + `RowVersion`), same reasoning as `Promotions`.

## ShippingMethods

Shipping (Milestone 7.3) - a named, admin-managed method (e.g. "Standard",
"Express") for a jurisdiction. Unlike `TaxRates` (one rate per category per
jurisdiction), several named methods can coexist for the same jurisdiction,
so uniqueness is on `Name` within the jurisdiction rather than the
jurisdiction alone - see `Architecture.md`'s Milestone 7.3 section. Cost
uses `Products.Weight`, a field that's existed unused since Milestone 2.4.
Consumed today only as an estimate against the store's configured default
jurisdiction (`Store:DefaultShippingCountryCode`/
`Store:DefaultShippingRegionCode`), same reasoning as `TaxRates`.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| Name | nvarchar(200) | No | Admin-facing label, e.g. "Standard Shipping" |
| Description | nvarchar(500) | Yes | |
| CountryCode | nvarchar(2) | No | ISO 3166-1 alpha-2 |
| RegionCode | nvarchar(10) | Yes | Sub-national; null means a whole-country method |
| BaseRate | decimal(18,2) | No | Flat handling fee |
| RatePerKg | decimal(18,2) | No | Added per kg of total order weight |
| FreeShippingThreshold | decimal(18,2) | Yes | Cost becomes 0 once the (pre-discount) subtotal meets this |
| EstimatedDeliveryDaysMin | int | Yes | |
| EstimatedDeliveryDaysMax | int | Yes | |
| DisplayOrder | int | No | |
| IsActive | bool | No | |

Indexes: unique on `(CountryCode, Name)` filtered where `RegionCode IS
NULL`, and unique on `(CountryCode, RegionCode, Name)` filtered where
`RegionCode IS NOT NULL` - same dual-filtered-index technique as
`TaxRates`/`Carts`. `AuditableEntity` (soft delete + `RowVersion`), same
reasoning as `TaxRates`/`Promotions`.

## Addresses

Addresses (Milestone 8.1) - a customer's saved address book, account-only
like `WishlistItems`. A single, unified list per user, not split into
separate shipping/billing tables - the customer picks one at checkout
(Milestone 8.2). `CountryCode`/`RegionCode` deliberately match
`TaxRates`/`ShippingMethods`' column shape, so a selected address can be
passed straight into the Checkout Calculation Service (Milestone 7.4) once
real checkout exists.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| UserId | nvarchar(450) | No | Plain string, not a navigation property - same reasoning as `WishlistItems`/`RecentlyViewedItems`/`RefreshTokens` |
| Label | nvarchar(50) | Yes | Customer's own nickname for the address, e.g. "Home", "Work" |
| FullName | nvarchar(200) | No | Recipient name - may differ from the account holder |
| Phone | nvarchar(30) | No | Delivery contact number |
| Line1 | nvarchar(200) | No | |
| Line2 | nvarchar(200) | Yes | |
| City | nvarchar(100) | No | |
| RegionCode | nvarchar(10) | Yes | Sub-national code, same convention as `TaxRates`/`ShippingMethods` |
| PostalCode | nvarchar(20) | No | |
| CountryCode | nvarchar(2) | No | ISO 3166-1 alpha-2 |
| IsDefault | bool | No | At most one per user - enforced by `AddressService`, not a DB constraint |
| CreatedAtUtc | datetime2 | No | |
| UpdatedAtUtc | datetime2 | No | |

Indexes: non-unique on `UserId` (lookup only - `IsDefault`'s at-most-one
invariant is a service-layer rule, not an index, since a customer's very
first address is always forced to be the default before any comparison is
possible). Plain `BaseEntity` - no soft delete or `RowVersion`, same
reasoning as `WishlistItems`/`Carts`/`CartItems`: a customer who deletes
their own address wants it gone, and there's no admin recycle bin for
personal data.

## Orders

Orders (Milestone 9.1) - a placed order, created once Checkout's server-side
revalidation succeeds. Everything a customer saw on the Review page is
frozen onto this row rather than referenced live: the shipping address is
fully copied (`Address` has no soft delete, so a customer deleting it later
must not corrupt past orders), and the applied shipping method/promotion are
snapshotted by name/amount even though their ids are also kept.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| OrderNumber | nvarchar(20) | No | Customer-facing identifier; unique index; not a raw database id |
| UserId | nvarchar(450) | No | Owning customer |
| Status | int (enum) | No | Pending/Paid/PaymentFailed/StockReservationFailed/Cancelled/Shipped/Delivered - see `OrderStatusTransitions` for legal transitions |
| IdempotencyKey | nvarchar(64) | No | Unique index; a duplicate PlaceOrder submission (double-click, retry) resolves to the same order rather than creating a second one |
| ShippingLabel | nvarchar(100) | Yes | The saved address's own label, snapshotted (e.g. "Home") |
| ShippingFullName / ShippingPhone / ShippingLine1 / ShippingLine2 / ShippingCity / ShippingRegionCode / ShippingPostalCode / ShippingCountryCode | nvarchar (various) | Line1/City/PostalCode/CountryCode/FullName/Phone required, Line2/RegionCode optional | Full copy of the address chosen at checkout |
| ShippingMethodId | int | Yes | FK to ShippingMethods, Restrict delete |
| ShippingMethodName | nvarchar(200) | No | Snapshot, survives the method being renamed/deactivated later |
| ShippingCost | decimal(18,2) | No | |
| PromotionId | int | Yes | FK to Promotions, Restrict delete |
| AppliedCouponCode | nvarchar(50) | Yes | Snapshot |
| AppliedPromotionName | nvarchar(200) | Yes | Snapshot |
| PromotionDiscountAmount | decimal(18,2) | No | |
| Subtotal | decimal(18,2) | No | |
| Tax | decimal(18,2) | No | |
| GrandTotal | decimal(18,2) | No | |
| StockIssueMessage | nvarchar(500) | Yes | Set only when Status is StockReservationFailed, naming the affected line |
| AdminNotes | nvarchar(2000) | Yes | Staff-only annotation (Milestone 10.2) - never shown to the customer |

Indexes: unique on `OrderNumber`, unique on `IdempotencyKey`, non-unique on
`UserId` and `Status`. `AuditableEntity` (soft delete + RowVersion).

## OrderItems

One purchased line, snapshotted the same way `PurchaseOrderItem` snapshots
`ProductName`/`ProductSku` - so order history stays accurate even if the
product is later renamed, re-priced, or deactivated.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| OrderId | int | No | FK to Orders, Cascade delete |
| ProductId | int | No | FK to Products, Restrict delete |
| ProductVariantId | int | Yes | FK to ProductVariants, Restrict delete |
| ProductName | nvarchar(200) | No | Snapshot |
| Sku | nvarchar(100) | No | Snapshot |
| VariantDescription | nvarchar(500) | Yes | Snapshot, e.g. "Color: Red" |
| ImagePath | nvarchar(500) | Yes | Snapshot |
| UnitPrice | decimal(18,2) | No | Snapshot; `LineTotal` is computed as `Quantity * UnitPrice`, never stored |
| Quantity | int | No | |

Indexes: non-unique on `OrderId`. `AuditableEntity`.

## Payments

The result of a single (simulated) charge attempt against an Order
(Milestone 9.2). Deliberately does **not** derive from `AuditableEntity` -
no soft delete, no `UpdatedAtUtc`, no `RowVersion` - the same reasoning
`StockMovements` uses: this row is written once, synchronously, with its
final outcome already known, and never updated afterward. A correction (a
refund) records a new, separate `Refund` transaction rather than editing
this one. Never stores the real card number - only a masked last 4 and the
detected brand, mirroring real PCI-compliant practice even in simulation.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| OrderId | int | No | FK to Orders, Cascade delete; unique index (at most one payment attempt per order) |
| MethodType | int (enum) | No | Only `CreditCard` exists today |
| Amount | decimal(18,2) | No | |
| Status | int (enum) | No | Succeeded/Declined |
| MaskedCardNumber | nvarchar(32) | No | e.g. "**** **** **** 4242" |
| CardBrand | nvarchar(50) | No | |
| DeclineReason | nvarchar(200) | Yes | Set only when Status is Declined |
| ProcessedAtUtc | datetime2 | No | |

Indexes: unique on `OrderId`. Plain `BaseEntity` - immutable financial
transaction record, never soft-deleted per `ISoftDeletable`'s own contract.

## Shipments

One shipment per order (Milestone 10.3) - a v1 scope choice, the same
reasoning `Payment`'s unique `OrderId` index uses: nothing upstream splits
an order into multiple packages, so a real multi-shipment model would be
speculative. Unlike `Payment`, has a real mutable lifecycle
(shipped -> delivered), so it derives from `AuditableEntity` rather than an
immutable, insert-once type.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| OrderId | int | No | FK to Orders, Cascade delete; unique index |
| Carrier | nvarchar(100) | No | Free text, entered by staff on "Mark shipped" |
| TrackingNumber | nvarchar(100) | No | Free text |
| ShippedAtUtc | datetime2 | No | |
| DeliveredAtUtc | datetime2 | Yes | Set by staff's "Mark delivered" action |

Indexes: unique on `OrderId`. `AuditableEntity`.

## Reviews

One review per (user, product), enforced via a unique index - the same
pattern `WishlistItem`'s toggle constraint uses. `AuditableEntity` rather
than a plain `BaseEntity`: unlike a wishlist bookmark or an immutable ledger
row, a review is substantive content that moderation needs to soft-delete
without losing the audit trail.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| ProductId | int | No | FK to Products, Cascade delete |
| UserId | nvarchar(450) | No | |
| Rating | int | No | 1-5 |
| Title | nvarchar(150) | Yes | |
| Body | nvarchar(2000) | No | |
| IsVerifiedPurchase | bool | No | Computed once at submission time from the customer's order history (a genuinely charged order - Paid/Shipped/Delivered/Cancelled) - a snapshot, not live-recomputed |

Indexes: unique on `(UserId, ProductId)`. `AuditableEntity`.

## ReviewReports

One customer's flag on a review, driving the admin moderation queue
(Milestone 12.2). `BaseEntity`, not `AuditableEntity` - a report is a
one-time event that's never edited, the same reasoning `WishlistItem` uses
for its own toggle records. Acting on the review (Dismiss or Remove) clears
its reports entirely rather than tracking a resolved/unresolved status -
there's no persistent moderation audit log in this scope, so a review with
reports is simply "still queued" and one with none is not.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| ReviewId | int | No | FK to Reviews, Cascade delete |
| ReporterUserId | nvarchar(450) | No | |
| Reason | int (enum) | No | |
| Comment | nvarchar(500) | Yes | |
| CreatedAtUtc | datetime2 | No | |

Indexes: unique on `(ReviewId, ReporterUserId)` - at most one report per
customer per review. Plain `BaseEntity`.

## ReturnRequests

A customer's request to return some or all of a Delivered order's items
(Milestone 13.1/13.2). `UserId` is a snapshot (mirrors `Review`'s own
denormalization), not just derivable via `Order.UserId`, so ownership
queries don't need a join. At most one open (Requested/Approved) request
per order is enforced at the service layer, not a DB-level filtered index.
There is no day-count return window anywhere in this app to enforce
(`Product.ReturnEligibility` is deliberately unstructured free text), so
eligibility is gated purely by the order having reached `Delivered`.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| OrderId | int | No | FK to Orders, Restrict delete |
| UserId | nvarchar(450) | No | Snapshot |
| Reason | int (enum) | No | Defective/WrongItem/NoLongerNeeded/NotAsDescribed/Other |
| Comment | nvarchar(1000) | Yes | |
| Status | int (enum) | No | Requested/Approved/Rejected/Refunded |
| DecidedAtUtc | datetime2 | Yes | Set when staff approve or reject |
| DecidedByUserId | nvarchar(450) | Yes | |
| RejectionReason | nvarchar(1000) | Yes | Set only when Status is Rejected |

`AuditableEntity`.

## ReturnRequestItems

Which order line(s), and how much of each, a return request covers.
`AuditableEntity`, mirroring `OrderItem`/`PurchaseOrderItem`'s own base-type
choice for a mutable, auditable order line.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| ReturnRequestId | int | No | FK to ReturnRequests, Cascade delete |
| OrderItemId | int | No | FK to OrderItems, Restrict delete |
| Quantity | int | No | |

`AuditableEntity`.

## Refunds

A refund issued once a return request's items are physically received back
(Milestone 13.3). Deliberately does **not** derive from `AuditableEntity`,
the same reasoning `Payment` itself uses: an immutable, insert-once
financial ledger entry. A refund is recorded as a new, separate transaction
rather than by editing the original `Payment` row.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| OrderId | int | No | FK to Orders, Restrict delete |
| ReturnRequestId | int | No | FK to ReturnRequests, Restrict delete; unique index (at most one refund per return request) |
| Amount | decimal(18,2) | No | |
| ProcessedAtUtc | datetime2 | No | |
| ProcessedByUserId | nvarchar(450) | Yes | The staff member who clicked "Mark received & refund" |

Indexes: unique on `ReturnRequestId`. Plain `BaseEntity` - immutable
financial transaction record.

## OutboxMessages

A durable "intent to send" row (Milestone 15.2), written by the same
`SaveChangesAsync` call that persists the business change it's about (a
paid `Order`, a password-reset request), so the two either both commit or
neither does. Uses the mutable `AuditableEntity` base like
`InventoryReservation`, since - unlike a pure ledger row such as `Payment` -
this one has a real Pending -> Processed/Failed lifecycle and gets updated
in place as delivery is attempted by `OutboxProcessingBackgroundService`
(Milestone 15.3).

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| Type | int (enum) | No | OrderConfirmationEmail/PasswordResetEmail |
| PayloadJson | nvarchar(max) | No | Serialized email model (order details, reset link, etc.) |
| Status | int (enum) | No | Pending/Processed/Failed |
| ProcessedAtUtc | datetime2 | Yes | |
| Attempts | int | No | Incremented on every processing attempt, including failed ones |
| LastError | nvarchar(2000) | Yes | |

Indexes: non-unique on `Status` - the processor's own lookup, every Pending
row. `AuditableEntity`.

## StoreSettings

A singleton row (Milestone 16.3) - exactly one is ever seeded/read - of
store-wide configuration that used to live only in `appsettings.json`'s
static `Store` section. Derives from `AuditableEntity` specifically for its
`RowVersion`: two admins editing this same shared row at once is a real
possibility ordinary per-record entities don't have to worry about.
`IsDeleted` is inherited but never meaningfully used - there is no recycle
bin for the one settings row.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key; always 1 in practice |
| StoreName | nvarchar(200) | No | |
| Currency | nvarchar(10) | No | e.g. "PKR" |
| DefaultCountry | nvarchar(100) | No | Display-only, distinct from the tax/shipping jurisdiction codes below |
| PricesIncludeTax | bit | No | |
| RecentlyViewedMaxItems | int | No | Caps the recently-viewed section's length |
| DefaultTaxCountryCode | nvarchar(2) | No | Store's default jurisdiction for the Cart page's Estimated tax line, before a real address is known |
| DefaultTaxRegionCode | nvarchar(10) | Yes | |
| DefaultShippingCountryCode | nvarchar(2) | No | Same purpose as the tax pair, for Estimated shipping |
| DefaultShippingRegionCode | nvarchar(10) | Yes | |

`AuditableEntity`.

## Soft delete and RowVersion

Every table above (except the pure join tables `ProductTagMappings`,
`ProductVariantAttributeValues`, `SupplierProducts`, `RecentlyViewedItems`,
`Carts`, `CartItems`, `WishlistItems`, `Addresses`, and `ReviewReports`; and
the immutable ledger tables `StockMovements`, `StockAdjustments`,
`GoodsReceipts`, `GoodsReceiptItems`, `Payments`, and `Refunds`) inherits
`AuditableEntity`:
`IsDeleted` (soft
delete, globally filtered out of normal queries) and `RowVersion` - a real SQL
Server `rowversion`/`timestamp` column and EF Core concurrency token (see
`ApplicationDbContext.OnModelCreating`'s `IsRowVersion()` configuration).
This was fixed during Milestone 2's own testing: `RowVersion` had existed
since Milestone 1 but was never actually wired up as a concurrency token, so
SQL Server never generated it and EF Core never checked it - optimistic
concurrency was silently a no-op until this was caught.
