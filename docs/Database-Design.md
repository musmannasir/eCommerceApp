# Database Design

## Status

The first migration, `InitialIdentityAndSecurity` (Milestone 1), adds ASP.NET
Core Identity's tables plus `RefreshTokens`, `UserSessions`, and
`SecurityAuditEvents` - see `Data-Dictionary.md` for full column detail. No
catalog/inventory/order tables exist yet; those arrive starting Milestone 2.
This document grows into a full ER-level description of the schema as they're
introduced.

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
