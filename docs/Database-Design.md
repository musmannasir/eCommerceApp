# Database Design

## Status

The first migration, `InitialIdentityAndSecurity` (Milestone 1), adds ASP.NET
Core Identity's tables plus `RefreshTokens`, `UserSessions`, and
`SecurityAuditEvents`. The second, `CatalogSchema` (Milestone 2), adds
`Categories`, `Brands`, `Products`, `ProductTags`/`ProductTagMappings`,
`ProductImages`, `ProductAttributes`/`ProductAttributeValues`,
`ProductVariants`/`ProductVariantAttributeValues`, and
`ProductSpecifications` - see `Data-Dictionary.md` for full column detail. No
inventory/order tables exist yet; those arrive starting Milestone 3. This
document grows into a full ER-level description of the schema as they're
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
`DbUpdateConcurrencyException`.
