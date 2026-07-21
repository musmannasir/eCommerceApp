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
| M5.1 - Product detail page | **Complete** | Public `/Product/{slug}` page: brand/category/breadcrumbs, image gallery with click-to-zoom modal, price/compare-at/discount%/tax indicator, aggregated stock status + low-stock warning, per-attribute variant selectors (reload-based resolution - see the M5.2 scope note below), quantity selector + disabled Add to Cart, SKU, description, specifications, warranty/returns/shipping, honest placeholders for ratings/reviews/frequently-bought-together/recently-viewed (recently-viewed and related products became real in M5.3). All product cards sitewide (home, listing pages) now link here, closing the loop left open since Milestone 4.1. |
| M5.2 - Variant resolution & pricing service | **Complete** | Live, no-reload variant switching: a client-side matrix (embedded per page load) disables dropdown options that can't form a real combination given the other selections; the actual switch calls a strict, server-authoritative `GET /Product/{slug}/Resolve` endpoint (rejects any variant that doesn't exist, isn't active, or doesn't belong to the product) and updates SKU/price/compare-at/discount/stock/image without a page reload. New central `IPricingService` (pure calculator, no DB/I-O) computes base/variant/final price, discount amount/%, and a config-driven tax-inclusive flag - promotion adjustment is always 0 for now (no Promotion entity until Milestone 7.1). `ProductDetailService` now uses it as the single source of truth instead of computing price/discount inline. |
| M5.3 - Recently viewed & recommendations | **Complete** | `IRecentlyViewedService` records a view on every `/Product/{slug}` load - a guest gets a single `HttpOnly` cookie of comma-separated product IDs (no PII), an authenticated user gets a DB row per `(user, product)` upserted and trimmed to `Store:RecentlyViewedMaxItems` (default 10) on every view. Lives in the **Web** project, not Infrastructure, since it needs `HttpContext` - the same reasoning as `ICurrentUserService`. "Related Products" on the product detail page and the home page's "Recently viewed" section are both now backed by real data (`IRecommendationService`'s two-pass category/brand/tag/price-range scoring, and the recently-viewed history respectively) instead of Milestone 4/5.1's placeholders. "Best sellers" and home-page "Recommended for you" remain honest placeholders - both need signals (order history, or a recommendation basis with no anchor product) that don't exist until later milestones. This closes out Milestone 5 in its entirety. |
| M6.1 - Cart core | **Complete - scope note** | No brief text was available for this sub-milestone in this session (see the completion report), so scope was agreed with the user as reasonable cart-core conventions: `Cart`/`CartItem` entities (one cart per authenticated user or guest token, one line per product-or-variant), add/update-quantity/remove/clear, stock validated against `InventoryItem` with the same untracked/backorder leniency the product detail page uses, and prices always resolved live via `IPricingService` (never snapshotted). Guest carts use an `HttpOnly` cookie (`CartGuestToken`); `ICartOwnerAccessor` (Web) resolves the owner per request the same way `RecentlyViewedService` resolves guest identity, but `CartService` itself stays Infrastructure-hosted since it takes a plain `CartOwner` and has no `HttpContext` dependency of its own. Add to Cart on the product detail page and quantity/remove/clear on the new `/Cart` page are AJAX, CSRF-protected via a request header (not a form field) since they're JSON POSTs - see `_Layout.cshtml`'s `csrf-token` meta tag. Cart merge on login and price/stock re-validation at checkout-adjacent points are explicitly Milestone 6.2's job, not this one's. |
| M6.2 - Cart merge & pricing integrity | **Complete - scope note** | Same as M6.1: no brief text was available this session, so scope was agreed as reasonable conventions for what the milestone's name implies. **Cart merge**: `ICartService.MergeGuestCartIntoUserCartAsync` runs right after a successful MVC login/register (`AccountController`) - if the user has no cart yet, the guest cart is simply reassigned to them; if they already have one, each guest line either increments a matching line (capped to current stock, never rejected outright - a login can't reasonably fail over a quantity conflict) or moves over as a new line, and the now-empty guest cart is deleted. The JWT API surface doesn't merge - it has no browser cookie to read a guest cart from. **Pricing/stock integrity**: `CartItem.PriceWhenAdded` is a new field storing the price at the moment a line was added or last explicitly touched (re-add, quantity update, or merge all re-stamp it) - purely for comparison, never for billing; `LineTotal` always uses the live price from `IPricingService`, same as M6.1. Every cart read flags `PriceChanged` (with the previous price) when the live price no longer matches, and flags `QuantityExceedsStock` when the line's quantity is now more than current available stock - both purely informational, neither one silently mutates the stored `Quantity`. |
| M6.3 - Wishlist + AJAX | **Complete - scope note** | Same as M6.1/M6.2: no brief text was available this session, so scope was agreed as reasonable conventions. **Account-only, unlike Cart** - a wishlist is meant to persist indefinitely and follow the customer across devices, which a guest cookie can't do, and it's a lower-frequency action than adding to cart where guest friction actually matters, so there's no guest wishlist at all. **Product-level only, no variant** - a lighter bookmark than a cart line; `WishlistItem` (`BaseEntity`, one row per `(UserId, ProductId)`, `Cascade` FK to `Products` since there's no variant FK to create a multi-cascade-path conflict). `IWishlistService.ToggleAsync` adds if not present, removes if present (idempotent); a product that's since become unpublished/inactive/deleted is silently excluded from the list, the same reasoning `RecentlyViewedService` uses (not Cart's "keep it visible but flagged" approach - a wishlist is browsing-adjacent, not a committed purchase intent). The AJAX toggle button appears only on the product detail page - not on every product card across every listing page, since that would mean touching every card-emitting Storefront service (`HomePageService`, `CatalogBrowseService`, `RecommendationService`, `RecentlyViewedService`) for a nice-to-have, which was judged out of scope for "wishlist works." Found and fixed along the way: an unauthenticated AJAX toggle request got a silently-followed 302-to-login instead of a detectable failure - `Program.cs` now returns a real 401 for requests carrying `Accept: application/json`/`X-Requested-With: XMLHttpRequest`, and `site.js`'s shared `postJson()` helper sends both. This closes out Milestone 6 in its entirety. |
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

## Bugs found and fixed during Milestone 5.1's own testing

Found and fixed during development, before manual verification:

- The `@attribute`-as-loop-variable-name Razor collision recurred for a
  *third* time, this time in the product detail page's variant-selector
  view. Same fix (renamed to `productAttribute`). After this occurrence a
  persistent memory note was saved (outside this repo, in the assistant's
  cross-session memory store) specifically to stop this from recurring a
  fourth time in a future milestone.
- A design misstep, caught and reverted before it compiled: the first draft
  of `ProductDetailAttributeDto`'s `SelectedValueId` (only knowable *after*
  variant resolution, which happens after the attribute list is first built)
  tried to solve this by subclassing the DTO record with a mutable shadow
  property (`new int? SelectedValueId { get; set; }`). This doesn't work -
  C# records don't allow a derived type to relax a base `init`-only property
  to a settable one via `new` hiding; it's a separate shadowing property, not
  a real override. Fixed by building attribute groups as plain tuples first,
  then constructing the final immutable `ProductDetailAttributeDto` list in
  one pass once the variant is resolved - no mutation needed at all.

No further production bugs were found during manual verification - variant
switching (both directions), the image zoom modal, all four content tabs,
breadcrumbs, and the branded 404 for an unknown product slug all worked as
designed on the first pass.

## Bugs found and fixed during Milestone 5.2's own testing

One real bug, caught only by manual browser testing - a good example of why
automated tests that assert on C# objects aren't proof the actual JSON wire
format is correct:

- The live variant-switch AJAX response left the stock badge blank after
  every switch. Root cause: `System.Text.Json` serializes enums as their
  underlying **integer** by default, not their name, unless a
  `JsonStringEnumConverter` is configured. `VariantResolutionDto.StockState`
  (a `ProductStockState` enum) was serialized as `0`/`1`/`2`/`3`, but the
  client-side JS looked the value up in a string-keyed object
  (`{ InStock: ..., LowStock: ... }`) - `stockBadges[0]` matched nothing, so
  the badge silently disappeared. All 30 automated tests covering this code
  path passed regardless, because they assert against the `ProductStockState`
  C# enum value directly and never touch the actual serialized JSON string -
  exactly the class of gap integration/manual testing exists to catch. Fixed
  by adding `[property: JsonConverter(typeof(JsonStringEnumConverter))]` to
  the `StockState` property (scoped to that one property, not a global JSON
  option change, to avoid affecting the unrelated `/api/v1/auth` JSON
  surface). Confirmed fixed by re-running the same manual variant-switch
  test and inspecting `stockBadgeContainer.innerHTML` directly.

## Bugs found and fixed during Milestone 5.3's own testing

No production bugs were found - manual verification (guest cookie round-trip
via a fresh request cycle, category-based recommendations after adding a
second product, and the home page's recently-viewed section) all worked as
designed on the first pass. One test-authoring pitfall was caught while
writing the automated coverage, not the product code: seeding two products
with the *default* selling price of `10` and expecting one to be excluded
from recommendations by category alone - the price-tolerance signal
(`SellingPrice` within +/-30%) silently qualified it anyway, since both
products priced identically fell inside the band regardless of category.
Fixed by giving the "should not match" product a price far outside the
tolerance window instead of relying on category being the only signal in
play.

## Bugs found and fixed during Milestone 6.1's own testing

One real bug, caught by the full regression run (not by the new tests
themselves, which all passed in isolation):

- `TestDatabase.ResetAsync`'s per-run cleanup script deletes every table's
  rows in a fixed order, and `Products` is deleted partway through. The new
  `CartItems` table has `Restrict` (not `Cascade`) foreign keys to `Products`
  and `ProductVariants` - the same reasoning `InventoryItemConfiguration`
  already established, and the only viable choice given a `CartItem` also
  reaches `Product` indirectly through `ProductVariant`, and SQL Server
  rejects multiple cascade paths to the same table. Once `CartFlowTests` left
  real `Carts`/`CartItems` rows in the shared test database, every other
  integration test's next run failed at cleanup time with a foreign key
  violation on `DELETE FROM Products` - 64 unrelated failures from one
  missing cleanup line. Fixed by adding `DELETE FROM CartItems; DELETE FROM
  Carts;` (and matching `DBCC CHECKIDENT` reseeds) before the existing
  `Products` cleanup in `TestDatabase.cs`. A reminder that a new
  Restrict-FK'd child table must be added to this script the same milestone
  it's introduced, not left for later.

## Bugs found and fixed during Milestone 6.3's own testing

One real bug, self-caught while writing the integration tests (a scenario
the manual browser check would have hit next, since it's exactly what the
product-page toggle button does):

- An anonymous `fetch()` POST to `/Wishlist/Toggle` (an `[Authorize]`
  action) got ASP.NET Core's default cookie-auth challenge: a `302` to
  `/Account/Login`, which `fetch()`'s default `redirect: 'follow'` silently
  follows - the call "succeeds" with a `200` and a login-page HTML body
  instead of the JSON the client code expects, so `response.json()` throws
  inside the `.catch()` instead of the intended "redirect to login" branch
  ever running. Fixed by overriding `CookieAuthenticationOptions.Events
  .OnRedirectToLogin` in `Program.cs` to return a real `401` when the
  request carries `Accept: application/json` or
  `X-Requested-With: XMLHttpRequest`, and adding both headers to `site.js`'s
  shared `postJson()` helper (used by Cart's endpoints too, so this also
  quietly hardens those against the same class of issue, even though Cart
  doesn't require authentication). Verified with a dedicated integration
  test asserting the real HTTP status is `401`, not a followed-redirect `200`.

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
