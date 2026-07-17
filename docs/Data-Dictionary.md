# Data Dictionary

Tables added by the `InitialIdentityAndSecurity` migration (Milestone 1) and
the `CatalogSchema` migration (Milestone 2).
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

## Soft delete and RowVersion

Every table above (except the pure join tables `ProductTagMappings` and
`ProductVariantAttributeValues`) inherits `AuditableEntity`: `IsDeleted` (soft
delete, globally filtered out of normal queries) and `RowVersion` - a real SQL
Server `rowversion`/`timestamp` column and EF Core concurrency token (see
`ApplicationDbContext.OnModelCreating`'s `IsRowVersion()` configuration).
This was fixed during Milestone 2's own testing: `RowVersion` had existed
since Milestone 1 but was never actually wired up as a concurrency token, so
SQL Server never generated it and EF Core never checked it - optimistic
concurrency was silently a no-op until this was caught.
