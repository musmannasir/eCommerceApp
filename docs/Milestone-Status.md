# Milestone Status

> Renumbered to the sub-milestone-granularity build prompt (2026-07-17). The
> underlying requirements are unchanged from the original 18-milestone
> document - see the audit note below - only the session-sized breakdown is
> new. F.1-F.3, M1.1-M1.3, and M2.1-M2.4 were built under the earlier,
> larger-grained structure and have been confirmed to satisfy every item in
> their new sub-milestone descriptions.

| Step | Status | Notes |
|---|---|---|
| F.1 - Solution, DI, EF Core, core abstractions | **Complete** | Clean-architecture scaffold, DI/EF Core base, `ApplicationDbContext`, auditable-entity + soft-delete abstractions, `IClock`/`ICurrentUserService`, Result/Error abstractions. |
| F.2 - Logging, error handling, health | **Complete** | Global MVC exception handling, API `ProblemDetails`, Serilog + correlation IDs, dev exception page, prod HSTS/HTTPS redirection, SQL Server health checks, `/health/live`, `/health/ready`. |
| F.3 - UI shell, tests, docs | **Complete** | Public + Admin Bootstrap 5 layouts, branded error/404/access-denied pages, all test projects + architecture tests + boot integration test, full `docs/*` set + `README.md`. |
| M1.1 - Identity domain, roles, admin seeding | **Complete** | `ApplicationUser` (profile fields, lockout tracking via Identity base, last-login, password-changed date), `RefreshToken`/`UserSession`/`SecurityAuditEvent` entities, 7 roles + 6 policies seeded, SuperAdmin seeded from `SeedAdmin:*` config. |
| M1.2 - MVC auth flows + cookie security | **Complete** | Register/login/logout/forgot/reset/change password, profile, revoke-all-sessions, access-denied; account lockout, unique/normalized email, open-redirect prevention, CSRF, secure cookie settings, generic login errors, security-event audit logs. |
| M1.3 - JWT API auth + refresh-token security | **Complete** | `/api/v1/auth` (register/login/refresh/logout/revoke-all/me), rotating refresh tokens hashed at rest, expiration, revocation, reuse detection (revokes full chain), rate limiting on auth endpoints. |
| M2.1 - Categories & Brands | **Complete** | Full field sets, unlimited nesting with cycle-detection (`CategoryService.WouldCreateCycleAsync`), searchable/sortable/paginated admin CRUD, soft delete + recycle bin for both. |
| M2.2 - Product core | **Complete** | Full `Product` field set, `ProductImage`, publish/unpublish workflow, admin list/create/edit, soft-delete only (no physical delete path exists). |
| M2.3 - Attributes & variants | **Complete** | `ProductAttribute`/`ProductAttributeValue`/`ProductVariant`/`ProductVariantAttributeValue`, duplicate-SKU and duplicate-combination rejection (`CombinationKey`), variant admin UI on the product edit screen. |
| M2.4 - Specs, SEO, tags, file upload hardening | **Complete** | `ProductSpecification`, `ProductTag`/`ProductTagMapping`, SEO fields on `Product` itself (see architectural note below), signature-validated (magic-byte) image upload via `IFileStorage`/`LocalFileStorage`, random filenames, path-traversal prevention, orphan cleanup. |
| M3.1 - Warehouse & stock ledger | **Complete** | `Warehouse` (multi-capable, one seeded as default via Admin UI), `InventoryItem` (one per purchasable product-or-variant per warehouse, computed `QuantityAvailable`, denormalized `StockStatus`), `StockMovement`/`StockAdjustment` (immutable ledger, no soft-delete/RowVersion), `InventoryReservation` (Active/Released/Consumed/Expired). Admin UI: warehouse CRUD, inventory overview, low/out-of-stock views, movement history, opening-stock recording, manual adjustment. Overselling prevented unless backorder allowed; negative on-hand rejected; RowVersion concurrency enforced. See report for details. |
| M3.2 - Suppliers | **Complete** | `Supplier` (full CRUD, soft delete + recycle bin) and `SupplierProduct` (plain join, hard-delete on unlink) linking suppliers to products with supplier SKU/cost/lead-time/preferred flag; admin CRUD + product-linking UI on the supplier edit page. |
| M3.3 - Purchase orders & goods receipt | **Complete** | `PurchaseOrder` (Draft→Submitted→Approved→PartiallyReceived/Received, or Cancelled - not cancellable once any goods received) with `PurchaseOrderItem` (product-level, matching `SupplierProduct`'s granularity); `GoodsReceipt`/`GoodsReceiptItem` (immutable receiving records, full+partial receipt, audited over-receipt override); receiving auto-creates/updates the matching `InventoryItem` and writes a `PurchaseReceipt` `StockMovement`, all in one DB transaction with RowVersion concurrency protection. Milestone 3 acceptance flow (supplier → PO → approve → receive full+partial → stock increase + linked movement → audited adjustment → low-stock view) verified end-to-end. |
| M4.1 - Layout, navigation, home page | **Complete** | Public layout wired to real catalog data via a `CategoryNavViewComponent` (category names shown, non-clickable until M4.2 builds the destination pages); sticky footer; home page composed by `IHomePageService` (hero/promo banners, featured categories, featured/new-arrival/discounted products - all Active+Published only). New admin-managed `HomePageBanner` entity + CRUD (image upload, soft delete) since hero/promo content must be admin-managed per the brief. Best-sellers/recommended/recently-viewed are honest placeholders pending Milestones 9/5. |
| M4.2 - Catalog listing pages | **Complete** | Public `CatalogController` serves `/Products` (all), `/Category/{slug}` (includes active descendant subcategories), `/Brand/{slug}`, `/Search` (baseline name/SKU/description/keywords substring match), and `/Brands` (brand index) - all sharing one listing view with grid/list toggle, pagination, active-filter chip + clear, and empty-result state. Category nav, home page featured-category cards, and product-card brand names are now real, clickable links (deferred from M4.1 pending this milestone). Product cards remain non-clickable (Milestone 5). Product cards also show a baseline "Out of stock" badge without hiding out-of-stock items (M4.3 adds an explicit hide-them filter). |
| M4.3 - Search, filters, sorting, performance | **Complete - scope note** | Filters (price range, category, subcategory, brand, stock availability, discounted, featured, attributes, new arrivals - combinable, persisted in query string), sorting (Relevance/Newest/Price asc+desc/Largest Discount/Name A-Z+Z-A), debounced AJAX search suggestions, and performance work (indexes, in-memory nav caching, lazy-loaded card images) are all implemented. **Rating filter/sort and "Best Selling" sort are deliberately not offered** - there is no rating data (Milestone 12) or order/sales history (Milestone 9) yet to filter/sort by, and a control that silently produced arbitrary results would be misleading. See the "Deferred filter/sort options" note below. |
| M5.1 - Product detail page | Not started | |
| M5.2 - Variant resolution & pricing service | Not started | |
| M5.3 - Recently viewed & recommendations | Not started | |
| M6.1 - Cart core | Not started | |
| M6.2 - Cart merge & pricing integrity | Not started | |
| M6.3 - Wishlist + AJAX | Not started | |
| M7.1 - Promotions & coupons | Not started | |
| M7.2 - Tax service | Not started | |
| M7.3 - Shipping | Not started | |
| M7.4 - Checkout calculation service | Not started | |
| M8.1 - Addresses | Not started | |
| M8.2 - Checkout flow UI | Not started | |
| M8.3 - Server-side revalidation & idempotency | Not started | |
| M9.1 - Order entities & snapshots | Not started | |
| M9.2 - Payments | Not started | |
| M9.3 - Stock reservation transaction | Not started | |
| M10.1 - Order queue UI | Not started | |
| M10.2 - Order detail & operations | Not started | |
| M10.3 - Shipment & centralized state machine | Not started | |
| M11.1 - Dashboard & order list | Not started | |
| M11.2 - Order detail, tracking, invoice | Not started | |
| M11.3 - Reorder | Not started | |
| M12.1 - Review submission & rating summary | Not started | |
| M12.2 - Moderation & abuse protection | Not started | |
| M13.1 - Cancellation | Not started | |
| M13.2 - Returns | Not started | |
| M13.3 - Refunds & restocking | Not started | |
| M14.1 - Ledger & dashboard | Not started | |
| M14.2 - Cash flow | Not started | |
| M14.3 - Reports & export | Not started | |
| M15.1 - Email abstraction & templates | Not started | |
| M15.2 - Transactional outbox | Not started | |
| M15.3 - Background jobs | Not started | |
| M16.1 - User management | Not started | |
| M16.2 - Audit logging | Not started | |
| M16.3 - Store configuration | Not started | |
| M17.1 - Security hardening | Not started | |
| M17.2 - Data protection & performance | Not started | |
| M17.3 - Reliability | Not started | |
| M18.1 - Test coverage & test-DB safety | Not started | |
| M18.2 - Documentation set | Not started | |
| M18.3 - Deployment package & final checks | Not started | |

## Deferred filter/sort options (Milestone 4.3) - flagged prominently

The brief's Milestone 4.3 lists "rating" as a filter dimension and "highest
rated"/"best selling" as sort options. **None of the three are offered in
this milestone's UI or query API.** This is a deliberate scope decision, not
an oversight:

- **Rating** (filter and sort): no `Review`/`Rating` entity or data exists
  yet - that's Milestone 12's scope. There is nothing to filter or sort by.
- **Best selling** (sort): no `Order`/`OrderItem` history exists yet - that's
  Milestone 9's scope. Same reasoning the home page's "Best sellers" section
  (Milestone 4.1) already used: rather than fake it with a proxy metric (which
  would misrepresent real sales data once it exists) or silently return
  arbitrary/unsorted results behind a control that claims to do something
  real, the option is not shown at all.

All other filters (price range, category, subcategory, brand, stock
availability, discounted, featured, attributes, new arrivals) and sort
options (relevance, newest, price ascending/descending, largest discount,
name A-Z/Z-A) from the brief are fully implemented. Revisit this note once
Milestones 9 and 12 land - adding the missing options then is a small,
additive change to `CatalogSortOption`/`CatalogBrowseQuery`, not a redesign.

## Architectural note carried over from Milestone 2

The build prompt (both the original and the restructured version) lists
`ProductSeoMetadata` as a distinct entity in the Milestone 2 entity list, but
also lists `MetaTitle`/`MetaDescription` directly as `Product` fields. Since
SEO metadata is a strict 1:1 relationship with a product, this was resolved
as an architectural judgment call: `MetaTitle`/`MetaDescription` live
directly on `Product` (see `src/ECommerceApp.Domain/Catalog/Product.cs`) and
no separate `ProductSeoMetadata` table exists. This was already true before
the prompt restructuring and is unchanged by it.

## Architectural note for Milestone 3.1

The brief describes `InventoryItem` as tracking "one per purchasable
variant," but Milestone 2's catalog model never requires a `Product` to have
`ProductVariant` rows - a simple product is fully purchasable via its own
`BaseSKU`. Rather than retrofit Milestone 2 to force every product to have at
least one variant (an unrequested change to already-completed, tested code),
`InventoryItem` carries a required `ProductId` plus an optional
`ProductVariantId`: null means the item tracks the product itself, non-null
means it tracks one specific variant. See `Database-Design.md` for the full
reasoning and the DB constraints that enforce it.

## Known deviations from the brief (approved by project owner)

- **Target framework**: `net10.0` instead of `net8.0` - this machine only has
  the .NET 10 SDK/runtime installed. See `Architecture.md` for the
  verification steps and reasoning.

## Known temporary conditions

None currently - the Foundation milestone's Admin Area exposure was resolved
by Milestone 1's role-gating (`Roles.StaffRolesCsv` on the Admin Area's
`HomeController`).

## Bugs found and fixed during Milestone 1's own testing

- Rate limiter was unpartitioned (global counter) - fixed to partition per
  client IP.
- Several `IConfiguration` reads (JWT bearer options, rate-limit thresholds)
  were captured eagerly before `WebApplicationBuilder.Build()`, so
  `WebApplicationFactory` test overrides were silently ignored - fixed by
  moving each read inside its lazy configuration callback. See
  `docs/Architecture.md` and `docs/Security.md` for detail.
- `ECommerceApp.IntegrationTests` needed
  `[assembly: CollectionBehavior(DisableTestParallelization = true)]` to
  avoid concurrent `WebApplicationFactory` instances racing on Serilog's
  shared static logger.

## Bugs found and fixed during Milestone 2's own testing

- `RowVersion` had existed since the Foundation milestone but was never
  configured as a real SQL Server concurrency token - optimistic concurrency
  was silently a no-op. Fixed with `IsRowVersion()` in
  `ApplicationDbContext.OnModelCreating`, applied to every entity
  implementing `IHasRowVersion`.
- A SQL Server "multiple cascade paths" migration failure
  (`Products` -> `ProductImages` both directly and via `ProductVariants`) -
  fixed by making the variant->image FK `NO ACTION` and detaching images in
  application code before removing a variant. See `Database-Design.md`.
- `ProductService.AddVariantAsync`'s re-query threw `NullReferenceException`
  (visible as a 500 in the Admin UI) from a missing `.Include()` chain - an
  Infrastructure.Tests unit test against the EF Core InMemory provider
  passed anyway due to change-tracker identity fixup masking it. Fixed and
  covered by a real end-to-end integration test. See `Architecture.md`.
- Two manual-testing false alarms traced to the curl/shell harness, not the
  app (multi-token pages breaking naive `grep`; `RequestMessage.RequestUri`
  not reflecting the post-redirect URL under `WebApplicationFactory`). See
  `Testing-Guide.md`.

## Bugs found and fixed during Milestone 3.1's own testing

No production bugs were found during manual testing this sub-milestone (all
golden-path and guardrail scenarios - opening stock, duplicate-tracking
rejection, adjustment above/below zero, low/out-of-stock detection - worked
as designed on the first manual pass). One real implementation issue was
caught by the automated test suite before it ever reached manual testing:

- `InventoryService`'s explicit `Database.BeginTransactionAsync()` calls
  (required by the brief's "use transactions ... throughout" for inventory)
  threw `InvalidOperationException` under the EF Core InMemory provider used
  by `Infrastructure.Tests`, which doesn't support real transactions. Fixed
  by only opening a transaction when `_dbContext.Database.IsRelational()` is
  true. See `Database-Design.md` and `Testing-Guide.md` for detail.

## Bugs found and fixed during Milestone 4.1's own testing

- A `HomePageBanner` with no image uploaded yet still rendered in the
  storefront carousel/promo grid with an empty `<img src>` (a broken image),
  even though the Admin edit page's own copy said the banner "will not
  appear on the home page until one is set." Fixed by filtering
  `ImagePath != null` in `HomePageService`'s hero/promo queries, and covered
  by a new test asserting an imageless banner is excluded. Found during
  manual verification, not by the automated suite (the original tests
  covered CRUD, not the storefront rendering path for an incomplete banner).
- `ApplicationStartupTests` used a placeholder, unreachable connection
  string, which was safe while nothing on its request paths touched the
  database. Milestone 4.1 broke that assumption: the public layout's
  category nav (rendered via `CategoryNavViewComponent` on every page,
  including the 404 and login pages) and the home page itself now query real
  catalog data unconditionally, so those tests started failing with 500s.
  Fixed by moving `ApplicationStartupTests` onto the same shared, real
  test-database fixture (`AuthTestFixture`) every other integration test
  class already uses. See `Testing-Guide.md`.

## Bugs found and fixed during Milestone 4.2's own testing

No production bugs were found - all golden-path and guardrail scenarios
(category descendant inclusion, brand/search filtering, pagination,
active-filter clearing, empty-result state, unknown-slug 404) worked as
designed on the first manual pass. One risky pattern was deliberately
verified rather than assumed: the product-card projection builds several EF
Core subqueries (primary-image lookup, out-of-stock check) inline as an
`Expression<Func<Product, HomeProductCardDto>>` specifically so they're
guaranteed translatable to SQL - Milestone 2's `AddVariantAsync` bug is a
standing reminder that an InMemory-provider test passing is not proof of
that. A dedicated integration test (`CatalogBrowseFlowTests`) drives all
four listing routes over real HTTP against the real SQL Server test
database and confirmed the projection translates and executes correctly.
One browser-automation false alarm: synthetic Enter keypresses in the
header search box didn't trigger the browser's native single-input-form
submit behavior in the testing tool, even though the rendered form
(`method="get" action="/Search"`) was correct - confirmed via
`form.requestSubmit()` and by navigating to the URL directly. Not an app bug.

## Bugs found and fixed during Milestone 4.3's own testing

Found and fixed during development, before manual verification:

- The same Razor `@attribute`-as-a-loop-variable-name collision documented
  in Milestone 2 recurred in the new filter-panel view
  (`@foreach (var attribute in Model.FilterOptions.Attributes)`), since
  `attribute` matches the reserved `@attribute` directive when followed by
  `.PropertyName`. Fixed by renaming the loop variable to `productAttribute`,
  same fix as before - a reminder to grep for this pattern before naming any
  new loop variable `attribute` in this codebase.
- The suggestions dropdown's rendering code initially built HTML by string
  concatenation with unescaped product/category names before injecting via
  `innerHTML` - a real XSS gap even though today's only source is
  admin-authored product data (defense-in-depth matters regardless of who
  currently controls the source). Fixed with a `textContent`-based
  `escapeHtml()` helper applied to every interpolated string before render,
  caught during self-review before it ever reached manual testing.

No further production bugs were found during manual verification - filters
(individually and combined), every sort option, the debounced suggestions
dropdown (including a full click-through to search results), category/list
view toggling with filter-state preservation, and the search-input-safety
check (a `<script>` tag in the search box rendered as inert text, confirmed
via console with no execution) all worked as designed on the first pass.

## Known EF Core warnings (benign, not yet addressed)

At startup: `Product` and `ProductAttributeValue` have global (soft-delete)
query filters and are the *required* end of a relationship with
`ProductTagMapping` and `ProductVariantAttributeValue` respectively.
Milestone 3.1 adds two more of the same class: `InventoryItem` (also
soft-delete-filtered) is the required end of a relationship with
`StockAdjustment` and `StockMovement`. Milestone 3.2 adds a fifth: `Product`
is the required end of its relationship with `SupplierProduct`, same as the
`ProductTagMapping` case. Milestone 3.3 adds two more: `PurchaseOrder` is the
required end of its relationship with `GoodsReceipt`, and `PurchaseOrderItem`
with `GoodsReceiptItem` - the same "soft-delete-filtered parent, immutable
non-filtered child" shape as the InventoryItem/StockMovement pair. All seven
are cosmetic for how the app actually queries (always parent-down, never
join-table/ledger-up), but are left as known warnings rather than silently
suppressed - worth a proper look if a future milestone ever queries those
tables directly as roots.
