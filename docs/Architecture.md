# Architecture

## Layers and dependency rules

```
ECommerceApp.Domain          no project references; no EF Core/MVC/SQL Server
ECommerceApp.Application     -> Domain only
ECommerceApp.Infrastructure  -> Application, Domain
ECommerceApp.Web             -> Application, Infrastructure
```

These rules are enforced by automated tests in
`tests/ECommerceApp.IntegrationTests/ArchitectureTests.cs`, which inspect each
compiled assembly's actual references (not just `.csproj` `ProjectReference`
entries) and fail the build if a lower layer starts depending on a higher one.

- **Domain** holds entities, value objects, and the `Result`/`Error` types.
  Zero third-party dependencies, so domain rules can be unit tested without a
  database, a web host, or mocks of infrastructure concerns.
- **Application** holds interfaces that Infrastructure/Web implement
  (`IClock`, `ICurrentUserService`, and per-milestone repository/service
  interfaces), plus FluentValidation validators and application services. No
  EF Core, no ASP.NET Core.
- **Infrastructure** holds `ApplicationDbContext`, EF Core configuration,
  health checks, and concrete implementations of Application interfaces that
  don't need the HTTP pipeline (e.g. `SystemClock`).
- **Web** is a single ASP.NET Core project containing both the MVC
  storefront/admin UI and the versioned Web API controllers under `/api/v1`.

## Storefront/API integration decision

The MVC controllers and the `/api/v1` Web API controllers both call
`Application`-layer services **in-process via dependency injection** - there
is no HTTP call from the MVC site to its own API. The two presentation layers
share one composition root (`Program.cs`) and one set of application
services; they differ only in how the caller authenticates:

- MVC: ASP.NET Core Identity cookie authentication + antiforgery tokens.
- API: JWT Bearer authentication (added in Milestone 1).

Rationale: looping the server-rendered site through its own HTTP API would
add latency and duplicate serialization for no benefit, since both run in the
same process. The `/api/v1` surface exists for external/future clients
(mobile apps, third parties), not as a backing service for the storefront.

## Identity placement (Milestone 1)

`ApplicationUser` (extends ASP.NET Core Identity's `IdentityUser`) lives in
**Infrastructure**, not Domain - Domain must stay free of any framework
dependency, and `IdentityUser` is a framework type. `RefreshToken`,
`UserSession`, and `SecurityAuditEvent` are plain Domain entities instead,
since they only need a `string UserId` reference, not an Identity base class.

Auth business logic (registration, login/lockout, token issuance/rotation,
audit logging) lives behind `IAuthService`, declared in **Application** as
primitive/DTO-typed methods, implemented by `AuthService` in
**Infrastructure** (where `UserManager`/`SignInManager` actually live).
Application never references `ApplicationUser` directly - this is what lets
`IAuthService` stay declared in Application without breaking the "Application
may reference only Domain" rule. Controllers (both MVC and API) depend on
`IAuthService`, not on Identity types, keeping them thin.

`SignInManager<ApplicationUser>` requires the full ASP.NET Core framework
(`IHttpContextAccessor`, authentication scheme provider), which a plain
classlib doesn't reference by default - `ECommerceApp.Infrastructure.csproj`
adds `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for this.
Web still owns the actual HTTP pipeline/middleware configuration.

## Catalog service pattern and file storage (Milestone 2)

`CategoryService`/`BrandService`/`ProductAttributeService`/`ProductService`
follow the same shape as `AuthService`: declared as interfaces in
Application, implemented in Infrastructure directly against
`ApplicationDbContext` (no separate repository layer - a repository
abstraction over EF Core, which is already a repository/unit-of-work
abstraction, would be indirection with no payoff here).

Image uploads go through `IFileStorage` (Application interface),
implemented by `LocalFileStorage` (Infrastructure) which writes to
`wwwroot/uploads/{category}/{random-guid}.{ext}`. The stored extension and
content-type are derived from the file's **signature** (magic bytes via
`ImageSignatureDetector`), never from the caller-supplied filename or
`Content-Type` header - both are trivially spoofable, and the brief requires
real content validation, not just extension checking. Filenames are always
random, and `DeleteAsync` only ever touches paths under `/uploads/`.

## Inventory service pattern and cross-module reuse (Milestone 3.1)

`InventoryService` follows the same shape as the Milestone 2 catalog
services: declared as `IInventoryService` in Application, implemented
directly against `ApplicationDbContext` in Infrastructure, no repository
layer. It lives in its own `Domain.Inventory`/`Application.Inventory`/
`Infrastructure.Inventory` namespace grouping rather than being folded into
`Catalog`, since inventory is a distinct bounded concept (warehouses, stock
levels, movements) that references the catalog (`Product`/`ProductVariant`)
rather than being part of it.

The Admin "record opening stock" screen needs a lightweight product+variant
picker list. Rather than reach into `ApplicationDbContext` from
`InventoryController` (which would violate "controllers hold no business
logic / no direct EF access") or loop `IProductService.GetByIdAsync` once per
product (an N+1 query pattern), one new read-only method -
`IProductService.GetPickerListAsync()` - was added to the existing catalog
service. This is treated as an additive, backward-compatible extension of
Milestone 2's service (no existing method's behavior changed) rather than a
scope violation of "no unrelated changes to other milestones' code."

## A materialization bug this milestone's manual testing caught

`ProductService.AddVariantAsync` re-queried the just-created variant with
`.Select(v => MapVariant(v))` directly against `IQueryable<ProductVariant>`.
`MapVariant` is a plain C# method - EF Core can't translate it to SQL, and
without an explicit `.Include()` chain for
`AttributeValues.ProductAttributeValue.ProductAttribute`, those navigations
were null, throwing `NullReferenceException` (visible as a 500 from the
Admin UI). The `Infrastructure.Tests` unit test for this exact path (against
the EF Core InMemory provider) **passed anyway**, because the test reused one
`DbContext` across several calls in the same test, and EF's change-tracker
identity-fixup silently wired up the navigations from entities already
tracked earlier in the test - something a real, per-request `DbContext`
scope never benefits from. Fixed by explicitly `.Include()`-ing the chain
before calling `MapVariant`, and covered by a real end-to-end integration
test (`ProductAdminFlowTests`) that drives the actual Admin UI over HTTP
against the real SQL Server test database, not the InMemory harness.

Lesson generalized: a passing InMemory-backed unit test is not proof that a
projection or navigation chain works against a real relational provider -
prefer the real test database (or at least a fresh, untracked `DbContext`
per operation) for anything that depends on `.Include()` being right.

## Storefront home page composition and the public layout (Milestone 4.1)

`HomePageService` (`Application.Storefront`/`Infrastructure.Storefront`)
follows the M3.1-established convention of querying `ApplicationDbContext`
directly rather than composing through `ICategoryService`/`IProductService`/
`IHomePageBannerService` - it has one method, `GetHomePageAsync()`, that
issues its own tailored, storefront-shaped projections (with primary image,
discount percentage, etc.) rather than reusing the Admin-grid-shaped
`ProductListItemDto`. `HomeController` itself stays thin: inject one service,
call one method, pass the result straight to the view.

**Category nav is real data, but not yet clickable.** `CategoryNavViewComponent`
renders live category names from `ICategoryService.GetTreeAsync()` (filtered
to active categories - `GetTreeAsync` itself returns the full tree, active
and inactive, for Admin's tree view), but every category is rendered as
plain non-interactive text, not an anchor. This follows the Foundation
milestone's "no unfinished active links" rule literally: the category
listing page these links would point to doesn't exist until Milestone 4.2.
The same reasoning extends to the home page's featured-category cards and
every product card (`_ProductCard.cshtml`) - product detail pages are
Milestone 5's scope, so none of them link anywhere yet. Only
`HomePageBanner.LinkUrl` renders as a real `<a href>`, since that URL is
admin-supplied content, not a system-generated link to a page this milestone
doesn't build.

**Hero banners and promo blocks are the one genuinely new admin-managed
entity this sub-milestone needed.** Featured categories/products reuse the
existing `IsFeatured` flags from Milestone 2; new arrivals and discounted
products are pure query filters (`PublishedAtUtc`/`CreatedAtUtc` ordering,
`CompareAtPrice > SellingPrice`) - none of that needed new schema. Hero
banners and promo blocks have no equivalent backing data, and the brief
explicitly requires them to be "admin-managed, not hardcoded," so
`HomePageBanner` + Admin CRUD (mirroring the `Brand` two-step
create-then-upload-image pattern) was added. Best sellers, by contrast, has
no honest way to be admin-managed *or* query-derived yet - it depends on
real order history that doesn't exist until Milestone 9 - so it renders a
plain "coming soon" message instead of being backed by a proxy metric that
would misrepresent real sales data later.

**A Foundation-era test assumption broke, correctly.** `ApplicationStartupTests`
originally ran against a placeholder, unreachable connection string,
documented as safe because nothing on its request paths touched the
database. That stopped being true here: `CategoryNavViewComponent` renders
on every page using the public `_Layout.cshtml` (unconditionally, including
the 404 page and the login page, which is also part of the public site), so
every one of those pages now needs a real, reachable database. Rather than
make the view component swallow DB failures defensively (which would hide
genuine errors in production too), the test class was moved onto the same
shared `AuthTestFixture` real-test-database fixture every other integration
test class already uses. This is treated as a natural, expected consequence
of the storefront becoming real rather than a design flaw to work around.

## Catalog listing pages (Milestone 4.2)

One public `CatalogController` backs `/Products`, `/Category/{slug}`,
`/Brand/{slug}`, and `/Search` - all four share one `CatalogListingViewModel`
and one `Index.cshtml` view, with `ICatalogBrowseService.BrowseAsync()`
taking a `CatalogBrowseMode` enum to decide which filter to apply. This
avoids four near-duplicate controllers/views for what is fundamentally the
same page (a paginated product grid) with a different starting filter.

**Category pages include active subcategories.** Visiting a parent category
shows products assigned directly to it *and* to every active descendant,
computed by loading all active categories once (the table is small) and
walking parent/child links in memory rather than a recursive SQL CTE -
simpler to read and test, and fast enough at this scale. An inactive
category (or one of its descendants) is invisible to this walk, same as the
rest of the catalog's active/published rules.

**The EF Core translation risk flagged in Milestone 4.1 was verified, not
just hoped to be fine.** The product-card projection (image lookup,
out-of-stock subquery) is built as an `Expression<Func<Product,
HomeProductCardDto>>` returned from a method - never a call to a helper
*inside* a `Select()` lambda, which EF Core cannot reliably translate. A
dedicated integration test (`CatalogBrowseFlowTests`) drives all four
listing routes over real HTTP against the real SQL Server test database
specifically to prove this translates and executes, not just that the
InMemory-backed `CatalogBrowseServiceTests` pass - the same "InMemory
passing isn't proof" lesson Milestone 2's `AddVariantAsync` bug taught.

**Out-of-stock baseline.** A product's card shows an "Out of stock" badge
if it has at least one `InventoryItem` row and none of them have available
stock or allow backorder - but it is never *excluded* from a listing.
Products with no inventory record at all (never stocked) are treated as
in-stock/unknown rather than penalized, since Milestone 2's publish
workflow doesn't require inventory setup first. An explicit filter to hide
out-of-stock products is Milestone 4.3's "stock availability" filter, not
this milestone's.

**Non-clickable links, partially resolved.** Milestone 4.1 deferred category
links because the destination didn't exist; this milestone builds that
destination, so the category nav, home page featured-category cards, and
every product card's brand name are now real `<a>` links. Products
themselves still aren't clickable anywhere - product detail pages are
Milestone 5's scope - so that non-clickable-until-M5 decision carries
forward unchanged.

## Search, filters, sorting, performance (Milestone 4.3)

**Deferred filter/sort options.** Rating (filter + sort) and "best selling"
(sort) are not offered - see `Milestone-Status.md`'s "Deferred filter/sort
options" note for the full reasoning (no backing data yet from Milestones 12
and 9 respectively). Flagged here too since it's a real, visible reduction
from the brief's literal option list, not a detail to bury in a status table.

**One request-binding model instead of a dozen action parameters.**
`CatalogFilterRequest` (`Page`, `View`, `Sort`, `MinPrice`, `MaxPrice`,
`CategoryId`, `BrandId`, `InStock`, `Discounted`, `Featured`, `NewArrivals`,
`Attr[]`) is bound once via `[FromQuery]` on all four listing actions
(`Index`/`Category`/`Brand`/`Search`), rather than each action declaring its
own long parameter list. ASP.NET Core's model binder handles the repeated
`?attr=1&attr=2` query-string convention for the array automatically.

**Link generation via a single `BuildUrl` method, not `asp-all-route-data`.**
Pagination, sort, and grid/list-toggle links all need to preserve every
active filter while overriding exactly one value. The tag helper's
`asp-all-route-data` was considered but doesn't handle the multi-valued
`attr` parameter cleanly (it expects a flat dictionary); `CatalogListingViewModel
.BuildUrl(page:, sort:, view:)` builds the full query string explicitly
instead, so attribute-filter state is never silently dropped by a pagination
click. This also fixed a real bug from Milestone 4.2's original hand-rolled
query strings: the search term wasn't URL-encoded, so a term containing `&`
or `#` would have corrupted the generated link.

**Faceted attribute filtering, not a flat OR.** Selected attribute-value IDs
are grouped by their parent `ProductAttribute` (one extra lookup query) so
that filtering correctly ANDs across different attributes (Color AND Size)
while ORing within one attribute (Red OR Blue) - the standard e-commerce
facet shape. The attribute/value list itself is global (all active
attributes, not scoped to what's actually present in the current result
set) rather than computed as true per-query facets - a deliberate
simplification; true faceting would need to run essentially the same query
once per attribute to know which values remain possible, which is real
additional complexity deferred rather than silently skipped.

**Caching for stable navigation data**, per the brief's explicit performance
requirement - see `CategoryNavViewComponent`'s own doc comment for the
Admin-bypasses-the-cache reasoning (this document's earlier "Storefront
home page composition" section covers the component's routing story).

**Responsive images, partially addressed.** Product/category card images use
`loading="lazy"` (skip the hero banner, which is above the fold) and
CSS-based sizing (`object-fit: cover` with fixed container dimensions). True
`srcset`/multi-resolution images would need a thumbnail-generation pipeline
- `LocalFileStorage` (Milestone 2) stores exactly the file that was
uploaded, nothing else - and building that pipeline (an image-processing
library, multiple stored variants, upload-time resizing) is real scope not
requested elsewhere in the brief. Deferred rather than half-built.

**The EF Core translation risk was checked again, not just trusted.**
Every new filter/sort branch, plus the suggestions endpoint, is exercised
against real SQL Server in `CatalogBrowseFlowTests` (all seven sort options,
a fully-combined filter request, and the JSON suggestions response) -
extending Milestone 4.2's same precaution rather than assuming the
established `Expression<Func<>>` pattern would keep working as the query
grew more complex.

## Product detail page (Milestone 5.1)

**M5.1/M5.2 scope split.** The brief's M5.1 bullet lists "variant selectors"
as a UI element; its M5.2 bullet separately owns "attribute selection
resolves exact variant... disables unavailable combinations... blocks
invalid combos from cart" plus a dedicated pricing service. This milestone
builds the former only: per-attribute `<select>` dropdowns inside one GET
form that auto-submits on change (small vanilla JS, not full AJAX), fully
reloading the page; the server resolves the selected combination to a real
variant (or falls back to the first active variant with a "not available"
notice if the combination doesn't exist). Live, no-reload resolution,
client-side disabling of invalid options, and the centralized pricing
service are Milestone 5.2's job.

**Variant resolution precedence:** an explicit `variantId` query parameter
wins if present; otherwise selected attribute-value IDs are combined via the
same `ProductVariant.BuildCombinationKey` used for duplicate-combination
prevention in Milestone 2, and matched against active variants' stored
`CombinationKey`. No match and no selection at all both fall back to the
first active variant (ordered by Id) - a product page should never render
with no price/SKU shown at all.

**Stock aggregation reuses the M4 "untracked = available" leniency.** If a
product (or the selected variant) has zero `InventoryItem` rows anywhere,
it's treated as InStock rather than penalized for stock never having been
recorded - the same reasoning already applied to the out-of-stock badge on
listing pages. Where inventory *is* tracked, on-hand/reserved are summed
across every warehouse (the storefront doesn't do warehouse selection
anywhere in the brief), and `Product.LowStockThreshold` - a single
product-level field, not per-variant - is reused as the low-stock cutoff
regardless of which variant is currently selected.

**A caught design mistake, not shipped:** the first draft tried to make
`ProductDetailAttributeDto.SelectedValueId` (unknown until after variant
resolution) settable by having a private subclass shadow the record's
`init`-only property with `new int? SelectedValueId { get; set; }`. This
doesn't compile the way it looks like it should - `new` hides, it doesn't
relax `init` to `set` on the same underlying property - so it was reverted
in favor of the obvious fix: collect attribute/value groups as plain tuples
first, and only construct the final immutable DTOs once the variant is
known. See `Milestone-Status.md`'s Milestone 5.1 bugs section.

**Honest placeholders, not fabricated data:** ratings summary, review
preview, and frequently-bought-together all show plain "coming in a later
milestone" text - no `Review`/`Rating` entity exists yet (Milestone 12), and
frequently-bought-together needs real basket co-occurrence data from orders
(Milestone 9) that doesn't exist either. Recently-viewed is the same
placeholder for now; Milestone 5.3 is where the real tracking mechanism
(cookie for guests, DB for authenticated customers) gets built.

**Closing the "non-clickable product" loop.** Every product card built since
Milestone 4.1 (`_ProductCard.cshtml`, used on the home page and every
listing page, plus the Catalog list-view rows) explicitly deferred linking
the product itself because the detail page didn't exist. This milestone
makes them real links to `/Product/{slug}`, using Bootstrap's
`stretched-link` pattern on the list-view rows specifically so the
already-present brand-name link (a sibling anchor, not nested - browsers
don't support nested `<a>` tags) keeps working independently.

## Variant resolution & pricing service (Milestone 5.2)

**Two resolution paths, one strict and one lenient, by design.** `Details()`
(page load, arbitrary/bookmarkable URL) stays lenient - an unmatched
combination falls back to the first active variant with a notice, since the
URL isn't something the app fully controls. `Resolve()` (the live AJAX
switch) is strict - it's the brief's "server revalidates every variant
selection" requirement made concrete: it rejects a variant that doesn't
exist, isn't active, or doesn't belong to the product, rather than falling
back to anything. In normal use the client-side disabling logic should
never let a customer construct a request that fails this check; reaching
the strict path with an invalid combination means something bypassed the
UI (a stale bookmark of a since-deactivated variant, a manually crafted
request), which is exactly when "revalidate, don't trust the client" earns
its keep.

**Client-side disabling without a round trip per hover.** The full active-
variant combination matrix (`variantId` + its attribute-value IDs) is
embedded as JSON on page load. Changing a dropdown re-evaluates, for every
option in every other dropdown, whether some variant combination still
contains that option's value together with everything currently selected
elsewhere - pure client-side set logic, no network call needed just to grey
out an option. Only once a selection change resolves to an *exact* variant
match does the client call `Resolve()`, and only that response (never the
embedded matrix) is used to update the visible price/stock/image - the
matrix only ever drives which options are clickable, never what's shown.

**`IPricingService` is deliberately a pure function, not a DB-backed
service.** "Central pricing service, single source of truth" (the brief's
own words) doesn't yet need any I/O: there's no `Promotion` entity
(Milestone 7.1) to look up and no real tax-rate engine (Milestone 7.2) to
query, so `Calculate(basePrice, baseCompareAtPrice, variantPrice,
variantCompareAtPrice)` takes plain values its callers already have loaded
and returns a `PriceResultDto` synchronously - fully unit-testable with no
mocking, and safe to inject anywhere (registered as a singleton) without
the cross-service-transaction concerns that shaped decisions elsewhere (see
Milestone 3.3's notes). `PromotionAdjustment` is hardcoded to 0 and
`IsTaxInclusive` reads a single `Store:PricesIncludeTax` config flag - both
are honest placeholders for real logic Milestones 7.1/7.2 will add, not
guesses at what that logic will look like.

**A bug that only manual testing caught.** `System.Text.Json` serializes
enums as integers by default, not names. The live-resolution JSON response
initially sent `stockState` as a bare number while the client JS expected a
string key - every automated test passed (they assert on the C# enum
directly, never the wire format), but the stock badge silently went blank
in the browser. Fixed with `[property: JsonConverter(typeof(JsonStringEnumConverter))]`
scoped to that one property. See `Milestone-Status.md`'s Milestone 5.2 bugs
section for the full account - a concrete reminder that this project's
"verify against real SQL Server, not just InMemory" discipline has a JSON
equivalent: verify the actual response shape a browser will parse, not just
the object a C# test constructs.

## Recently viewed & recommendations (Milestone 5.3)

**`IRecentlyViewedService` lives in the Web project, not Infrastructure.**
Every other Storefront service (`HomePageService`, `CatalogBrowseService`,
`ProductDetailService`, `RecommendationService`) queries
`ApplicationDbContext` directly and needs nothing else. Recently-viewed
tracking is different: a guest customer has no `UserId` to key a DB row on,
so the only place to persist their history is a cookie, and cookies require
`HttpContext` - something Infrastructure deliberately has no dependency on.
This is exactly the shape `ICurrentUserService`/`CurrentUserService` already
solved (Web owns anything HttpContext-dependent, registered directly in
`Program.cs` rather than through `AddInfrastructure()`), so
`RecentlyViewedService` follows the same precedent. `ProductDetailService`
and `HomePageService` (both in Infrastructure) still depend on it, but only
through the `IRecentlyViewedService` abstraction defined in Application -
Infrastructure never references the Web project, and DI resolves the
concrete Web-project implementation at runtime regardless of which project
registered it.

**Guest tracking is a single cookie, not one cookie per product.** A
`HttpOnly`, `SameSite=Lax` cookie (`Secure` when not in Development, mirroring
the existing Identity cookie policy) named `RecentlyViewed` holds a
comma-separated, most-recent-first list of product IDs - nothing else, no
name, no session token, nothing personally identifying. `Expires` is 90 days
and `IsEssential=false` (it's a convenience feature, not something the app
requires to function, so it doesn't bypass a "reject non-essential cookies"
consent choice). Re-viewing a product already in the list moves it to the
front instead of duplicating it, and the list is trimmed to
`Store:RecentlyViewedMaxItems` (default 10) on every view.

**Authenticated tracking is a DB row per `(user, product)`, upserted and
trimmed the same way.** `RecentlyViewedItem` is a plain `BaseEntity` (no
soft-delete, no optimistic-concurrency need - the same reasoning already
applied to `ProductTagMapping`, `SupplierProduct`, and the stock-ledger
entities), with `UserId` as a plain `string` field rather than a navigation
property, since `ApplicationUser` lives in Infrastructure and Domain cannot
reference it (the same pattern `RefreshToken`/`UserSession` already
established in Milestone 1). A product that's since been unpublished,
deactivated, or soft-deleted is simply filtered out by the same
`IsActive`/`IsPublished` query every other Storefront read applies - the
history silently forgets it rather than erroring or showing a broken card.

**"Recommendations v1" scores candidates, it doesn't rank by popularity.**
`RecommendationService` runs two passes: pass one scores every active,
published candidate (excluding the source product) with simple arithmetic -
same category (+3), same brand (+2), selling price within +/-30% of the
source (+1), and one point per shared `ProductTag` - filters out anything
scoring 0, orders by score descending, and takes the requested count as a
lean anonymous projection. Pass two re-queries just those winning IDs
through the same inline `Expression<Func<Product, HomeProductCardDto>>`
projection every other Storefront service uses (see the EF Core translation
note below), then re-sorts to match pass one's score order, since a
`Contains()` filter doesn't preserve it. **"Best selling" is deliberately not
one of the signals** - there's no `Order`/`OrderItem` history until
Milestone 9, and a signal that always contributes zero would be dead weight,
not a real feature; the interface (`GetRecommendationsAsync(productId,
count)`) doesn't need to change when that signal is eventually added, only
the scoring inside the implementation.

**What upgraded and what stayed a placeholder.** The product detail page's
"Related Products" section, previously a same-category-only query built in
Milestone 5.1, now sources from `IRecommendationService` instead - the DTO
field and the view are unchanged, only the query behind it got smarter. The
home page's "Recently viewed" section, an honest placeholder since
Milestone 4.1, is now backed by the same `IRecentlyViewedService`. Home
page's "Recommended for you" and both pages' "Best sellers" **remain
placeholders**: best sellers still needs order history (Milestone 9), and a
home-page "recommended for you" has no anchor product to score against -
`IRecommendationService` scores relative to a specific product, which is
exactly what exists on a product detail page and exactly what doesn't exist
on the home page. Inventing a different, unscoped signal wasn't part of this
milestone's brief, so it stays an honest gap rather than a guess.

## Cart core (Milestone 6.1)

**No brief text was available for this sub-milestone in this session** (the
original 18-milestone document was pasted early in a long-running session and
had scrolled out of context by the time M6.1 started; it was never saved as a
file in the repo). Rather than guess at exact requirements, the user was
asked and explicitly agreed to proceed from reasonable cart-core conventions:
add/update/remove/clear line items, a guest cart via a cookie and an
authenticated cart in the database, quantity validated against stock, totals
via the existing `IPricingService`, with cart merge-on-login and
price/stock re-validation at checkout-adjacent points explicitly deferred to
Milestone 6.2 (not this one's job) and wishlist deferred to Milestone 6.3.

**`Cart`/`CartItem` follow the InventoryItem precedent closely.** A `Cart` is
owned by exactly one of `UserId` or `GuestToken` (two filtered unique
indexes, never both/neither), created lazily on the first item added - not
every anonymous visitor gets a row, only one who actually adds something.
`CartItem` has `Restrict` (not `Cascade`) foreign keys to both `Product` and
`ProductVariant` - the exact same reasoning `InventoryItemConfiguration`
already established for Milestone 3.1 (a product is never physically
deleted in this app; and having both a direct `Product` FK and an indirect
one via `ProductVariant -> Product` rules out `Cascade` on both anyway, since
SQL Server rejects multiple cascade paths to the same table). The "one line
per product-or-variant per cart" rule reuses the same pair of filtered unique
indexes InventoryItem uses for "one purchasable unit per warehouse".

**Billing never uses a stored price.** Every read resolves the *current*
price via `IPricingService.Calculate(...)`, the same single source of truth
the product detail page uses. (Milestone 6.2 later adds a `PriceWhenAdded`
snapshot field to `CartItem`, but purely to *detect and flag* a price change
for the customer - `LineTotal` still always comes from the live calculation,
never from that stored value.)

**`ICartOwnerAccessor` lives in Web, `CartService` stays in Infrastructure -
a deliberate split from the Milestone 5.3 precedent.** `RecentlyViewedService`
needed to move to Web *entirely* because every one of its operations touches
`HttpContext` (reading/writing the guest cookie). Cart's HttpContext
dependency is much narrower: only "who owns this request's cart" needs
`HttpContext`, and that's a single, reusable piece of logic
(`ICartOwnerAccessor.GetOrCreateOwner()`/`TryGetOwner()`, in Web) that
resolves a plain `CartOwner` value up front. Once that value exists,
everything else - stock checks, EF queries, price calculation - has zero
HttpContext dependency, so `CartService` stays Infrastructure-hosted like
every other Storefront service, taking `CartOwner` as a parameter instead of
reaching for `HttpContext` itself. `TryGetOwner()` (read-only, never sets a
cookie) versus `GetOrCreateOwner()` (write path, mints a guest token if
needed) mirrors `RecentlyViewedService`'s "don't cookie a visitor who never
writes anything" discipline - `CartSummaryViewComponent` renders on every
single page via the layout and must never hand out a cart cookie just
because someone loaded a page.

**A cart line for a since-unpublished/deactivated/soft-deleted product stays
visible, unlike recently-viewed history.** Recently-viewed silently drops an
item that's no longer available - it's just browsing history, and a
customer doesn't need to know or care. A cart is different: a customer
expects to see exactly what's in it, including something that became
unavailable after they added it, so they can consciously remove it rather
than have it vanish. `CartService` resolves item DISPLAY data (name, slug,
image) via `Products.IgnoreQueryFilters()` (bypassing the soft-delete filter
for that one query only) so a soft-deleted product's line still renders, but
computes `IsAvailable` independently (active, published, not deleted, and -
if a variant - that variant active too) and excludes unavailable lines from
`Subtotal`/`TotalItemCount`. Only `RemoveItemAsync` is ever allowed on an
unavailable line; `UpdateQuantityAsync` rejects it.

**Stock validation duplicates `ProductDetailService`'s aggregation logic
rather than sharing it**, consistent with this codebase's established
convention (see Milestone 5's notes) of each Storefront service owning its
own `DbContext` access and its own small projections instead of introducing
a shared abstraction prematurely. The same untracked-inventory and
backorder leniency applies: no `InventoryItem` rows means unlimited
quantity is allowed; `AllowBackorder` on any matching row means the quantity
cap is skipped entirely rather than rejected.

**CSRF via a request header, not a form field - Cart's mutations are the
first AJAX-POST flow in this app.** Every prior AJAX endpoint (search
suggestions, M5.2's live variant resolver) was GET-only, so CSRF never came
up. Cart's Add/UpdateQuantity/Remove/Clear are JSON POSTs with no `<form>`
around them, so the antiforgery token has to travel as a header instead.
`Program.cs` configures `AddAntiforgery(options => options.HeaderName =
"X-CSRF-TOKEN")`; `_Layout.cshtml` calls `IAntiforgery.GetAndStoreTokens
(Context)` (the same call `@Html.AntiForgeryToken()` makes internally,
just rendered into a `<meta name="csrf-token">` tag instead of a hidden
form field) on every single page load, so the token is always present and
always valid regardless of which page a cart action originates from;
`site.js`'s `postJson()` helper reads it and attaches the header
automatically.

**A test-database cleanup gap this surfaced, not a product bug**: adding
`CartItems`' `Restrict` FKs to `Products` meant `TestDatabase.ResetAsync`'s
per-run cleanup script needed a matching `DELETE FROM CartItems; DELETE FROM
Carts;` before its existing `Products` cleanup, or every integration test
after `CartFlowTests` first ran would fail at cleanup with a foreign key
violation. See `Milestone-Status.md`'s Milestone 6.1 bugs section.

## Cart merge & pricing integrity (Milestone 6.2)

**Same brief-text gap as Milestone 6.1** - no spec was available for this
sub-milestone either, so scope was agreed with the user as reasonable
conventions implied by the name: fold a guest's cart into their account on
sign-in, and re-validate a cart's price/stock assumptions whenever it's
read, since both a customer and their catalog can change between when an
item was added and when they come back to check out.

**Merge runs in `AccountController`, not `ICartOwnerAccessor` or
`CartService` itself.** Neither of those has (or should have) any notion of
"a sign-in just happened" - that's an MVC-controller-level event.
`AccountController.Login`/`Register` call `MergeGuestCartAsync` immediately
after `SignInManager.SignInAsync(...)` succeeds, using
`ICartOwnerAccessor.TryGetGuestToken()` - a cookie read with no auth-state
branching, deliberately distinct from `TryGetOwner()`/`GetOrCreateOwner()`.
This distinction matters because of an ASP.NET Core cookie-auth quirk:
`SignInAsync` sets a response cookie, but `HttpContext.User` in the *same*
request doesn't reflect it until the *next* request - so `TryGetOwner()`,
which branches on `User.Identity.IsAuthenticated`, would still see an
anonymous user immediately after login and go looking at the wrong branch.
Reading the guest cookie unconditionally sidesteps that entirely. The JWT
API surface (`/api/v1/auth`) doesn't merge anything - it's bearer-token
auth with no browser cookie to read a guest cart from in the first place.

**Two merge paths, chosen by whether the user already has a cart.** If they
don't, the guest cart is just reassigned (`GuestToken = null; UserId =
theirs`) - no line-by-line work needed. If they do, each guest line either
increments a matching `(ProductId, ProductVariantId)` line in the user's
cart, or moves over as a new line if there's no match; either way the
now-empty guest cart row is deleted afterward. A combined quantity is
**capped to current stock, never rejected** - a sign-in is not a user
action the customer can retry or be blocked on, so the merge always
succeeds, and any resulting stock conflict surfaces afterward through the
same `QuantityExceedsStock` signal a plain cart read produces.

**`PriceWhenAdded` is a snapshot for comparison only - `LineTotal` still
never uses it.** Every write path that represents "the customer is looking
at today's price right now" (adding a line, incrementing an existing one,
an explicit quantity update, or a merge) re-stamps `PriceWhenAdded` to the
current live price - there is deliberately no "which of the two prices
wins" logic for a merge, since re-stamping to the live price sidesteps the
question entirely. A plain cart read never touches it: `BuildCartDtoAsync`
just compares the stored value against the live price and sets
`PriceChanged` (with `PreviousUnitPrice`) when they differ, letting the
customer see that something changed since they added it, without ever
charging the stale number.

**`QuantityExceedsStock` is informational, not self-correcting.** An
earlier draft of this milestone considered auto-clamping a cart line's
`Quantity` down to current stock whenever a read found it too high (e.g.
another customer bought the last few units after this one added theirs to
their cart). That was deliberately rejected: silently rewriting a value a
customer explicitly chose is a worse surprise than a warning banner,
and it would have made `GetCartAsync` - a read - had first started
mutating the database as a side effect, hurting testability and the
principle of least astonishment for no real benefit before checkout
(Milestone 8) exists to actually act on it. Instead, `BuildCartDtoAsync`
flags the discrepancy (current `Quantity` vs. `AvailableQuantity`) and
leaves the stored row untouched; the customer decides whether to reduce it
via the same `UpdateQuantityAsync` path that already re-validates against
stock.

## Wishlist (Milestone 6.3)

**Same brief-text gap as Milestones 6.1/6.2** - scope was agreed with the
user as reasonable conventions implied by the name, closing out Milestone 6.

**Account-only, deliberately not a Cart-style guest feature.** Every other
Storefront feature that needs per-visitor identity (recently-viewed, cart)
supports a guest via a cookie, so it's worth being explicit about why
Wishlist doesn't: a wishlist is meant to persist indefinitely and follow a
customer across devices, which a cookie fundamentally can't do, and it's a
much lower-frequency action than adding to cart - guest checkout friction
is a real, well-documented cart-abandonment driver, but nobody abandons a
purchase because saving something for later required an account. Real
stores (Amazon, Target) make the same call. `WishlistController` is
`[Authorize]`-gated wholesale rather than branching per-action.

**Product-level only, no variant - a lighter bookmark than a cart line.**
`WishlistItem` (`BaseEntity`, one row per `(UserId, ProductId)`, `Cascade`
FK to `Products`) has no `ProductVariantId` at all. Cart needs variant
granularity because it represents a specific purchasable thing the
customer is about to pay for; a wishlist is "I'm interested in this
product," and variant selection naturally happens later if the customer
moves it into their cart. `Cascade` (not `Restrict`) is safe and correct
here, unlike `CartItem`'s `Restrict`, precisely *because* there's no second
FK to `ProductVariant` creating a multi-cascade-path conflict - the same
shape `RecentlyViewedItemConfiguration` already uses.

**Unavailable items are silently dropped, not flagged like Cart's.** A
product that's since been unpublished, deactivated, or soft-deleted just
disappears from `GetWishlistAsync`'s results - the same reasoning
`RecentlyViewedService` already uses, not Cart's "keep it visible with a
badge" approach. The distinction is what each list represents: a cart line
is a customer's stated intent to buy something specific *right now*, so
losing it silently would be a real problem; a wishlist is closer to
browsing history with a save button, where quietly forgetting something
that's no longer purchasable is the less surprising behavior.

**The toggle button is scoped to the product detail page only - not every
product card sitewide.** A heart/toggle icon on every card across the home
page, every listing page, and search results would be the more complete
real-world feature, but it requires knowing each card's wishlist state up
front, which means threading `IWishlistService` through every
`HomeProductCardDto`-emitting service (`HomePageService`,
`CatalogBrowseService`, `RecommendationService`, `RecentlyViewedService`) -
a change to four already-completed Storefront services for a nice-to-have,
not "wishlist works." `ProductDetailDto.IsWishlisted` only touches the one
DTO that already has a per-request, per-product context to hang it on.
Sitewide card-level toggles are a reasonable candidate for later polish,
not this sub-milestone.

**Toggle, not separate Add/Remove, is the primary write path** - `IWishlist
Service.ToggleAsync` adds if absent, removes if present, matching the
heart-icon interaction pattern the button implements. A dedicated
`RemoveItemAsync` still exists for the wishlist page's explicit Remove
button, where "toggle" would be a confusing name for an action that only
ever removes.

**A real bug this milestone's own tests caught, not the product's business
logic**: an anonymous `fetch()` POST to `/Wishlist/Toggle` received ASP.NET
Core's default cookie-auth behavior - a `302` redirect to the login page -
which `fetch()`'s default `redirect: 'follow'` silently follows, resolving
with a `200` status and a login-page HTML body instead of anything the
client-side code could recognize as "you need to sign in." Fixed by
overriding `CookieAuthenticationOptions.Events.OnRedirectToLogin` in
`Program.cs`: if the request carries `Accept: application/json` or
`X-Requested-With: XMLHttpRequest`, respond `401` directly instead of
redirecting; `site.js`'s shared `postJson()` helper (used by Cart's
endpoints too) now sends both headers on every AJAX call, so this also
hardens Cart's endpoints against the same class of issue even though Cart
itself doesn't require authentication. See `Milestone-Status.md`'s
Milestone 6.3 bugs section for the full account.

## Promotions & coupons (Milestone 7.1)

**Same brief-text gap as Milestones 6.1-6.3** - scope was agreed with the
user as reasonable conventions implied by the name.

**Automatic vs. code-based, but only code-based is reachable this
milestone.** `Promotion.CouponCode` is nullable by design - an automatic
promotion (null code) applies with no customer action, a code-based one
requires the customer to type it in. `IPromotionService.FindApplicable
PromotionAsync` is a coupon-code lookup, so it can only ever find a
code-based promotion; an automatic one is fully admin-creatable (CRUD,
validation, the works) but nothing in this milestone ever evaluates it
against a cart. This is a deliberate scope cut, not an oversight: auto-
applying promotions raises a precedence question - which one wins if
several automatic promotions could apply to the same cart at once - that
this milestone's "at most one promotion per cart, no stacking" v1 rule
doesn't answer on its own, and answering it needs more design than a brief-
less sub-milestone justifies. Flagged here, not silently shipped.

**`MaxTotalUses`/`MaxUsesPerCustomer` are schema fields only, not
enforced.** Both are stored and shown in the admin form, but nothing
decrements or checks them. The reason is structural, not an oversight:
"enforced" requires counting *completed* uses, and the only signal
available until `Order` entities exist (Milestone 9) is "a cart currently
has this promotion applied" - which isn't a completed purchase. Enforcing
against that signal would let an abandoned cart (customer applies a
limited-use code, then never checks out) permanently consume one of its
uses. Revisit once Milestone 9 gives a real "this order was placed"
event to count against.

**Re-validated on every cart read, not just at apply-time - same pattern
Cart already uses for unavailable items.** `Cart.AppliedPromotionId` is
just an FK; `CartService.BuildCartDtoAsync` calls `IPromotionService.
ValidateAppliedPromotionAsync` on every read and silently clears it (no
error, no notice - the discount just stops appearing) if the promotion has
since expired, been deactivated, or its scope no longer matches anything
in the cart (e.g. the customer removed the one line a category-scoped
coupon applied to). This mirrors exactly how a soft-deleted product's cart
line is handled - the state can go stale between requests, so every read
re-derives truth instead of trusting what was true when it was applied.
`FindApplicablePromotionAsync` (code lookup, used by `ApplyCouponAsync`)
and `ValidateAppliedPromotionAsync` (id lookup, used by the read path)
share one private `Evaluate` method in `PromotionService` so the two paths
can never drift apart on what "valid" means.

**Scope determines the eligible amount a discount is computed against, not
the whole cart.** `PromotionScopeType.EntireOrder` discounts the full
subtotal; `Category`/`Brand`/`Product` only discount the sum of matching
cart lines (via a lean `PromotionCartLine(ProductId, CategoryId, BrandId,
LineTotal)` DTO - decoupled from Cart's domain model the same way
`IPricingService` takes raw scalars instead of entities). If no lines
match the scope, the promotion is rejected outright ("doesn't qualify"),
not silently applied with a zero discount. `MinimumOrderAmount`, however,
is always checked against the full subtotal regardless of scope - a
minimum-spend threshold is a statement about the whole order, not about
how much of it happens to be discounted.

**A discount can never exceed what it's discounting.** A `FixedAmount`
discount is capped to the eligible amount (a $50-off code on a $20
qualifying line only takes $20 off, never generates a negative total for
that scope), and a `Percentage` discount is additionally capped by
`MaxDiscountAmount` when set. Both caps apply in `Evaluate` before the
discount is ever handed back to the caller.

**Admin CRUD reuses `Policies.CanManageCatalog`**, matching Home Page
Banners - there's no separate Marketing policy, and `PromotionsController`
mirrors `HomePageBannersController`'s shape exactly (Index/Create/Edit/
Deactivate/Activate/Delete/Restore, soft delete + recycle bin). The scope
picker's Category/Brand/Product dropdowns reuse `ICategoryService.
GetAllActiveAsync`/`IBrandService.GetAllActiveAsync`/`IProductService.
GetPickerListAsync` - the same lean lookups `ProductsController` already
uses for its own Category/Brand dropdowns.

## Tax service (Milestone 7.2)

**Same brief-text gap as Milestones 6.1-7.1** - scope was agreed with the
user as reasonable conventions implied by the name.

**`TaxRate` models a jurisdiction + category, not a real customer
destination.** `(CountryCode, RegionCode?, TaxCategory)` maps to a
percentage; `RegionCode` null means the rate applies to the whole country,
and a region-specific rate for the same country/category takes precedence
over the country-wide one when both exist. Two filtered unique indexes
enforce this - a plain composite unique index would let the same
`CountryCode`+`TaxCategory` combination repeat indefinitely with
`RegionCode` left `NULL` each time, since SQL Server treats every row's
`NULL` as distinct from every other; this is the exact same problem, and
the exact same fix, Cart's `UserId`/`GuestToken` pair already solved.

**`TaxCategory` matches `Product.TaxCategory` by plain string equality
(case-insensitive), not a shared FK or enum.** Both fields stay free-text,
per the Data-Dictionary's pre-existing note (written back in Milestone 2)
that a structured tax-category model doesn't exist yet. This is a
deliberate, documented coupling-by-convention: a typo in either an admin's
Tax Rate category or a product's Tax Category silently produces no match
(zero tax, not an error) rather than a validation failure, since there's
no foreign key to enforce agreement between them. Worth tightening later
(e.g. a shared enum or a picker sourced from distinct in-use categories)
but out of scope for this sub-milestone.

**No real destination exists to calculate against - `Address` doesn't
arrive until Milestone 8.1.** This shapes the whole milestone's scope: the
Checkout Calculation Service (Milestone 7.4) is what will eventually
combine Tax + Shipping + Promotion into a final, destination-accurate
order total once a real shipping address exists. Until then, `ITaxService`
splits into two methods with two different audiences:
- `CalculateTaxAsync(amount, category, countryCode, regionCode)` is
  destination-agnostic and takes an explicit jurisdiction - ready for
  Milestone 8's real checkout to call with an actual address, unchanged.
- `CalculateEstimatedTaxAsync(lines)` is a convenience wrapper that reads
  the store's configured default jurisdiction (`Store:DefaultTaxCountryCode`/
  `Store:DefaultTaxRegionCode`, the same "config-driven store default"
  convention `PricingService`'s `Store:PricesIncludeTax` flag already
  uses) and is the only method actually wired up this milestone - it
  powers the Cart page's "Estimated tax" line. Both methods live on the
  same `ITaxService`/`TaxService` rather than splitting into two
  interfaces, since the estimate is just the general method looped with a
  fixed jurisdiction, not a fundamentally different calculation.

**The estimate is computed on pre-discount line totals, not what
`Total` (Subtotal minus PromotionDiscount) already discounts.** Allocating
a cart-level Promotion discount across lines for tax purposes - some
jurisdictions tax the discounted price, some don't, and a
category/brand/product-scoped promotion only discounts *some* lines -
is real complexity that belongs to the Checkout Calculation Service
(Milestone 7.4), which will have a complete, final order to reason about,
not an estimate. `CartDto.EstimatedTax` is clearly a preview, and the
existing "Tax and shipping calculated at checkout" note stays untouched
precisely because of this.

**`EstimatedTaxRateConfigured` distinguishes "nothing configured yet" from
a genuine 0% rate** - `CartService` doesn't show the estimated-tax line at
all when it's `false`, so an admin who hasn't set up any tax rates doesn't
accidentally communicate "this store charges no tax" to customers. It's
`true` if *any* line's category had a configured rate, even if others
didn't - a partial estimate is still more honest than none. A non-taxable
product (`Product.IsTaxable = false`) is simply excluded from the lines
passed to `CalculateEstimatedTaxAsync` in the first place, mirroring how
`CartService` already excludes unavailable lines from `Subtotal`.

**Admin CRUD reuses `Policies.CanManageCatalog`** - there's no dedicated
Checkout/Finance policy, and `TaxRatesController` mirrors
`PromotionsController`'s shape exactly (Index/Create/Edit/Deactivate/
Activate/Delete/Restore, soft delete + recycle bin). Its nav entry
introduces a new "Checkout" sidebar section (previously Tax Rates would
have had no natural home among Catalog/Inventory/Marketing) - a
deliberately forward-looking structural choice, since Milestone 7.3's
Shipping Rates admin UI will have an obvious place to land next to it.

**Bug found and fixed post-milestone (during Milestone 8.3's manual
checkout verification)**: `RateConflictsAsync`'s duplicate check queried
through the normal `DbSet`, which respects the global soft-delete filter -
so a previously-deleted rate's `(CountryCode, RegionCode, TaxCategory)`
combination looked "free" to the app. But the unique indexes backing that
combination (`IX_TaxRates_CountryCode_TaxCategory` /
`IX_TaxRates_CountryCode_RegionCode_TaxCategory`) have no `IsDeleted`
filter - a soft-deleted row still permanently occupies its natural key at
the database level. The result: creating a rate that matched a previously-
deleted one passed the app's own check, then failed with a raw, unhandled
`DbUpdateException` at `SaveChangesAsync` - a confusing 500 instead of
either succeeding or explaining what actually happened. Fixed by having the
conflict check itself use `IgnoreQueryFilters()` (matching what the
database actually enforces) and returning a specific message pointing the
admin at the Deleted list when the conflict is with a soft-deleted row,
rather than the generic "already exists" message.

## Shipping (Milestone 7.3)

**Same brief-text gap as Milestones 6.1-7.2** - scope was agreed with the
user as reasonable conventions implied by the name.

**A weight-based cost model, using a field that's sat unused since
Milestone 2.4.** `Product.Weight`/`Length`/`Width`/`Height` were added
during the catalog milestone for exactly this kind of downstream use, but
nothing consumed them until now. Cost is `BaseRate + RatePerKg *
totalOrderWeight`, where the order's total weight sums `Product.Weight *
Quantity` across the cart's available lines - a genuinely useful
calculation, not just a copy of Tax's flat-lookup shape, and one that puts
the dimension fields to their first real use. A line whose product has no
recorded weight contributes 0kg rather than blocking the estimate - the
same leniency `CartService.GetStockAsync` already applies to untracked
inventory (treat the missing signal as the permissive default, not a hard
stop).

**Several named methods can coexist per jurisdiction - `ShippingMethod`'s
uniqueness constraint reflects that, unlike `TaxRate`'s.** A store might
offer both "Standard" and "Express" shipping to the same country; TaxRate
never needed this (only one rate can sensibly apply to a given category in
a given jurisdiction). So `ShippingMethod`'s two filtered unique indexes
key on `(CountryCode, Name)` / `(CountryCode, RegionCode, Name)` - Name
must be unique *within* a jurisdiction, but the jurisdiction itself isn't
exclusive to one method the way a TaxRate row is. `GetAvailableShipping
OptionsAsync` reflects this directly: it returns every active method
matching the destination (both a whole-country method and a region-specific
one, if both exist - they're different named services, not competing
rates for the same thing), not a single winner.

**Same "estimate only" scope boundary as Tax, for the same reason -
no real destination exists until Milestone 8.1, and no method-picker UI
exists until Milestone 8.2.** `IShippingService` splits the same way
`ITaxService` did:
- `GetAvailableShippingOptionsAsync(weight, subtotal, countryCode,
  regionCode)` is destination-explicit and returns every option with its
  computed cost - ready for Milestone 8.2's checkout method picker to call
  with a real address, unchanged.
- `CalculateEstimatedShippingAsync(weight, subtotal)` reads the store's
  configured default jurisdiction (`Store:DefaultShippingCountryCode`/
  `Store:DefaultShippingRegionCode` - independent config keys from Tax's,
  since a store's assumed shipping origin/coverage and its assumed tax
  jurisdiction aren't necessarily the same policy decision even though
  both currently default to the same value) and returns the *cheapest*
  option - the only sensible single-number summary before any method
  picker exists to let the customer choose. This is the only consumer
  wired up this milestone, powering the Cart page's "Estimated shipping"
  line.

**The free-shipping threshold is checked against the pre-discount
subtotal, mirroring Tax's simplification exactly and for the same
reason** - a real post-discount check requires knowing how a cart-level
Promotion discount allocates across lines, which is the Checkout
Calculation Service's job (Milestone 7.4), not this estimate's.
`EstimatedShippingRateConfigured` distinguishes "no method configured for
this jurisdiction at all" from a genuine free/zero-cost method, the same
reasoning as `EstimatedTaxRateConfigured` - the Cart page hides the line
entirely rather than showing a possibly-misleading "Free" when nobody's
configured shipping yet.

**A real bug this milestone's own tests caught**: `TestDatabase.ResetAsync`
(the integration test suite's shared, real-SQL-Server test database reset,
run once per collection) had never been updated to clear `Promotions`,
`TaxRates`, or `ShippingMethods` when those tables were introduced across
Milestones 7.1-7.3 - exactly the "add a new table to this script the same
milestone it's introduced" reminder already on file from Milestone 6.3's
report, missed three times in a row. A `ShippingMethod` integration test
saw a method left over from an earlier test run and failed on a rerun (not
the same run it was created in) - `TaxRate`/`Promotion` tests happened to
dodge the same class of collision because each test could pick a unique
`TaxCategory`/coupon code, an extra dimension `ShippingMethod`'s
`(CountryCode, RegionCode)` lookup key doesn't have. Fixed by adding all
three tables' cleanup (and identity reseeds) to the script; a planned "no
shipping method configured" integration test for the app's one real
default jurisdiction was dropped rather than chased further, since there's
no per-test-randomizable dimension available to isolate it and the
underlying "no rate configured" logic is already fully covered by
`ShippingServiceTests`/`CartServiceTests` against a fresh InMemory database
per test.

**Admin CRUD reuses `Policies.CanManageCatalog`** and sits in the
"Checkout" nav section (introduced in Milestone 7.2) next to Tax Rates -
`ShippingMethodsController` mirrors `TaxRatesController`'s shape exactly.

**Bug found and fixed post-milestone (during Milestone 8.3's manual
checkout verification)**: the same soft-delete-vs-unique-index mismatch
described in Tax service's section above applies here too -
`NameConflictsAsync` didn't see soft-deleted rows, but
`IX_ShippingMethods_CountryCode_Name`/`IX_ShippingMethods_CountryCode_RegionCode_Name`
still enforce uniqueness against them, so recreating a method under a
previously-deleted name+jurisdiction threw an unhandled `DbUpdateException`
instead of succeeding or explaining why. Same fix: the conflict check now
uses `IgnoreQueryFilters()` and returns a message pointing at the Deleted
list when the conflict is with a soft-deleted row.

## Checkout Calculation Service (Milestone 7.4)

**Same brief-text gap as Milestones 6.1-7.3** - scope was agreed with the
user as reasonable conventions implied by the name.

**Closes the exact gap Tax and Shipping each explicitly deferred to this
milestone.** Both M7.2's "Estimated tax" and M7.3's "Estimated shipping"
lines were computed against the store's **pre-discount** line totals/
subtotal, with an explicit note that allocating a cart-level Promotion
discount across lines for tax/shipping purposes was this milestone's job.
`ICheckoutCalculationService` is the orchestrator `IPricingService` already
plays for a single product's price, but for a whole cart: it composes
`IPromotionService` + `ITaxService` + `IShippingService` into one final
total, computed against the **post-discount** amount.

**`PromotionApplicationDto` gained `LineDiscounts`** - one entry per input
line, same order, summing exactly to `DiscountAmount` (a line outside the
promotion's scope gets 0). Computed inside `PromotionService.Evaluate` via
a rounding-safe proportional allocation: each eligible line gets a share
proportional to its fraction of the eligible amount, except the *last*
eligible line, which takes the remainder instead of its own rounded share -
guaranteeing the allocations always sum to exactly the discount amount
regardless of rounding (the same "largest remainder" trick, applied at
allocation time rather than after the fact). This lets the Checkout
Calculation Service know exactly how much of a cart-level discount applies
to each line without duplicating the scope-matching logic
(`EntireOrder`/`Category`/`Brand`/`Product`) that already lives in
`PromotionService`.

**Same "estimate only" scope boundary as Tax/Shipping, for the same
reason** - no real destination exists until Milestone 8.1's Addresses, and
no checkout UI exists until Milestone 8.2. `ICheckoutCalculationService`
splits the same way `ITaxService`/`IShippingService` did:
- `CalculateAsync(lines, appliedPromotionId, taxCountryCode, taxRegionCode,
  shippingCountryCode, shippingRegionCode, selectedShippingMethodId)` is
  destination-explicit - it sums per-line tax via
  `ITaxService.CalculateTaxAsync` and picks the cheapest
  `IShippingService.GetAvailableShippingOptionsAsync` option unless one is
  explicitly selected (defaults to `null`, since there's no method-picker
  UI yet). Ready for Milestone 8.2's checkout, but has no real consumer
  yet.
- `CalculateEstimatedAsync(lines, appliedPromotionId)` is the store-default-
  jurisdiction convenience wrapper that now powers the Cart page - it
  reuses `ITaxService.CalculateEstimatedTaxAsync`/
  `IShippingService.CalculateEstimatedShippingAsync` directly (no new
  config-reading duplication needed) by simply passing **post-discount**
  amounts into them: each taxable line's `LineTotal - lineDiscounts[i]` for
  tax, and the post-discount subtotal for shipping's free-threshold check.

**Never fails - an invalid or missing applied promotion is simply treated
as "no discount," a pure calculator with no side effects**, mirroring
`IPricingService`'s design. `CheckoutCalculationService`'s internal
`ResolveDiscountAsync` calls `IPromotionService.ValidateAppliedPromotionAsync`
itself to get `LineDiscounts` for allocation - a deliberate, accepted minor
inefficiency (`CartService.ResolveAppliedPromotionAsync` already validated
the same promotion once for its own display/clearing purposes, so this is a
second, redundant read-only query) in favor of keeping
`CheckoutCalculationService` correct standalone rather than coupling its
public contract to a caller's pre-computed discount. The caller (e.g.
`CartService`) remains solely responsible for actually clearing an invalid
promotion from persisted state - this service only calculates.

**`CartDto` gained `EstimatedGrandTotal`** (`Total + EstimatedTax +
EstimatedShipping`, equivalently `CheckoutCalculationResult.GrandTotal`) -
still just an estimate, same reasoning as the two components it combines.
`CartService.BuildCartDtoAsync` now calls
`ICheckoutCalculationService.CalculateEstimatedAsync` instead of
`ITaxService`/`IShippingService` directly; `Total`'s existing meaning
(`Subtotal - PromotionDiscount`, no tax/shipping) is unchanged.

**Manually verified end-to-end**: a coupon that drops the cart's subtotal
from above a configured free-shipping threshold to below it now correctly
starts charging shipping instead of incorrectly staying free, and the
estimated tax line drops to reflect the post-discount amount instead of
staying flat - exactly the bug the pre-M7.4 architecture had baked in by
design (documented, not accidental, in M7.2/M7.3's own notes above).

## Addresses (Milestone 8.1)

**Same brief-text gap as Milestones 6.1-7.4** - scope was agreed with the
user as reasonable conventions implied by the name.

**A single, unified address book, not separate shipping/billing entity
types.** `Address` holds one flat record per saved address; a customer picks
whichever one they want at checkout (Milestone 8.2) rather than the app
enforcing a shipping-vs-billing split up front. This is a deliberate v1
simplification - real stores often do split them - but nothing here blocks
adding an `AddressType` dimension later if a later milestone needs it.

**`CountryCode`/`RegionCode` deliberately mirror `TaxRate`/`ShippingMethod`'s
shape exactly** (same field names, same "code, not free-text region name"
convention) - so once Milestone 8.2 wires up real checkout, a selected
`Address` can be passed straight into
`ICheckoutCalculationService.CalculateAsync` (Milestone 7.4) as
`taxCountryCode`/`taxRegionCode`/`shippingCountryCode`/`shippingRegionCode`
with zero translation. `City`/`PostalCode`/`Line1`/`Line2` stay human-entered
free text, since nothing needs to match or filter on them the way
Tax/Shipping match on country/region codes.

**Plain `BaseEntity`, no soft delete or `RowVersion`** - the same convention
`Cart`/`CartItem`/`WishlistItem` already established for customer-owned
personal records, as opposed to `AuditableEntity`'s recoverable/audited shape
used for admin-managed catalog/business records (Category, Promotion,
TaxRate, etc.). A customer who deletes their own address wants it gone; there
is no admin recycle bin for personal data, and no concurrent-edit scenario
worth guarding against for a single user's own address book.

**`IsDefault` is a service-layer invariant, not a DB constraint** - the same
choice this session made for Cart's single-applied-promotion rule. Two rules
`AddressService` enforces that a unique index couldn't express on its own:
- A customer's very first saved address is *always* the default, regardless
  of what the create request's `IsDefault` flag says - there's nothing to
  compare it against yet, and leaving a customer with zero default addresses
  immediately after saving their first one would be a confusing empty state.
- Deleting the current default address leaves **no** default at all, rather
  than silently promoting another address to fill the gap - the customer
  picks the new default explicitly via "Set as default," the same "don't
  guess on the customer's behalf" reasoning `CartService` applies to an
  invalidated promotion (clear it, don't substitute something else for it).

**Account-only, like Wishlist - every `IAddressService` method scopes its
query by `UserId`.** An id that exists but belongs to a different user
returns `NotFound`, identical to an id that doesn't exist at all - never
`Forbidden` - so a customer probing address ids can't learn whether *any*
address exists at that id, only whether one exists that's theirs. Verified
over a real HTTP round trip (`AddressFlowTests`), not just in-process:
another customer's `Edit`/`Delete` requests against someone else's address
id are checked to actually fail at the real controller/service boundary, not
just asserted against the service directly.

**Classic server-rendered MVC forms, not AJAX** - unlike Cart/Wishlist's
single-value toggle actions (quantity, coupon code, wishlist heart), an
address has many fields, so `AddressesController`/`Views/Addresses` mirror
`AccountController`'s `ChangePassword` form pattern (GET renders a
`ViewModel` with `DataAnnotations`, POST validates via `ModelState` first and
the `Application` layer's FluentValidation validators second, redirects with
`TempData` on success) rather than Cart/Wishlist's JSON-endpoint-plus-header-
CSRF-token pattern. Linked from the Profile page ("Manage addresses") next to
"Change password," not the header nav - an infrequent action, unlike
Cart/Wishlist's frequent, badge-worthy visibility.

**Same "add cleanup for every new table the same milestone it's introduced"
reminder from Milestones 6.3/7.3, caught proactively this time**:
`Address.UserId` is a plain string field, not a real database foreign key to
`AspNetUsers` - Domain has no dependency on Infrastructure's `ApplicationUser`
(the same reason `WishlistItem.UserId` and `Cart.UserId` are also plain
strings) - so deleting a test user does **not** cascade-delete their
addresses the way `WishlistItem`'s real FK-to-`Product` cascade incidentally
does. `TestDatabase.ResetAsync` now explicitly clears `Addresses` before this
could cause the exact cross-run leftover-row collision Milestone 7.3's
`ShippingMethod` bug already demonstrated once.

## Checkout Flow UI (Milestone 8.2)

**Same brief-text gap as Milestones 6.1-8.1** - scope was agreed with the
user as reasonable conventions implied by the name.

**Order placement itself is explicitly out of scope.** `Order` entities
don't exist until Milestone 9.1, so there is nothing for a "place order"
action to actually create yet. This milestone builds the real checkout
*flow* - address selection, shipping method selection, and a final review
with real destination-based totals - but Review's "Place order" button
stays disabled with an explanatory tooltip, exactly the placeholder pattern
the Cart page's own Checkout button used from Milestone 6.1 until this
milestone replaced it with a real link into this flow.

**A stateless, three-step flow carried entirely via query string** -
`CheckoutController`'s `Index` (address), `Shipping` (`?addressId=`), and
`Review` (`?addressId=&shippingMethodId=`) - rather than session or
TempData-backed wizard state. Every step re-derives everything it needs
(the cart's lines, the address book, the available shipping options) from
its query-string inputs, so there is no server-side state to keep in sync,
expire, or leak across tabs; the tradeoff is a full page load between
steps rather than a single-page wizard, which matches every other
classic-MVC-form flow in the app.

**Account-only (`[Authorize]`), a direct consequence of Address's own
scope decision.** Address (Milestone 8.1) has no guest concept, so there is
nothing for a guest to select at checkout - a guest with items in their
cart who clicks Checkout is redirected to log in like any other
`[Authorize]` page, and their guest cart already merges into their account
on login (Milestone 6.2's cart-merge flow), so nothing is lost by the
detour.

**One address serves both the tax and shipping jurisdiction** - consistent
with Address's "no shipping/billing split" decision (Milestone 8.1) - so
this milestone is `ICheckoutCalculationService.CalculateAsync`'s (Milestone
7.4) first real, destination-explicit consumer. Its store-default-
jurisdiction convenience wrapper, `CalculateEstimatedAsync`, is unchanged
and remains what the Cart page's "Estimated tax"/"Estimated shipping"
lines use - those stay honest estimates precisely because Checkout is
where the real, address-specific numbers now live.

**`ICartService` gained `GetCheckoutInputAsync`**, returning the cart's
currently-available lines (as `CheckoutLineDto`) plus the resolved applied
promotion id - exactly the input `CalculateAsync` needs. Rather than
duplicating the "build per-line DTOs from Cart+Product data" logic a third
time (it already existed once inline in `BuildCartDtoAsync` and once in
`BuildPromotionLinesAsync`), `CartService` was refactored to share one
`ComputeAvailableLines` helper across all three call sites - a
`Result`-returning method that fails with `"cart.empty"` when there is
nothing available to check out, the same error code `ApplyCouponAsync`
already used for the equivalent case.

**Guard rails redirect to the right earlier step rather than erroring**:
- An empty cart redirects to the Cart page.
- A customer with items but no saved address redirects to
  `Addresses/Create` - which gained `returnUrl` support (mirroring
  `AccountController`'s `ReturnUrl` pattern, including the same
  `Url.IsLocalUrl` open-redirect check) specifically so saving their first
  address lands them back in Checkout instead of the address book's own
  index page.
- An address id that doesn't belong to the current customer redirects back
  to address selection (`IAddressService.GetByIdAsync`'s existing
  ownership-scoped `NotFound` already makes this fall out for free - no new
  authorization logic needed).
- A `shippingMethodId` that doesn't match any option available for the
  selected address's jurisdiction - whether from URL tampering or because
  the cart/address changed between steps - redirects back to the Shipping
  step. This reuses `CalculateAsync`'s existing `ShippingRateConfigured =
  false` signal (previously only meaningful for "nothing configured for
  this jurisdiction at all") rather than adding a second validation path;
  both cases converge on the same, already-correct Shipping page, which
  either lists real options to choose from or explains that none exist for
  this address.

**Manually verified end-to-end** against a real destination (not the Cart
page's store-default-jurisdiction estimate): address -> shipping -> review
computing the correct subtotal/tax/shipping/total, and - critically - a
coupon discount correctly reducing both the taxed amount and the shipping
free-threshold check's post-discount subtotal, the exact post-discount
behavior Milestone 7.4 built `CalculateAsync` to guarantee.

## Server-side revalidation & idempotency (Milestone 8.3)

**Same brief-text gap as Milestones 6.1-8.2** - scope was agreed with the
user as a concrete, non-speculative reading of the milestone name, given it
sits **before `Order` entities exist** (Milestone 9.1) and therefore has no
real order row to revalidate against or make idempotent yet.

**Server-side revalidation was almost entirely already true by
construction.** Every earlier milestone in this app deliberately computes
price, promotion, tax, and shipping fresh from live data on every read
rather than trusting anything the client sends back - `CalculateAsync`
(Milestone 7.4) re-derives the whole total from the cart's current contents
and the address's current jurisdiction every single time it's called, with
no client-supplied total ever trusted. The one genuine gap: **stock
sufficiency was never checked inside the Checkout flow itself** - only
informationally flagged on the Cart page via `CartItemDto.QuantityExceedsStock`
(Milestone 6.2), which a customer could silently ignore and proceed anyway.
`CheckoutController` now guards against this at two points using that exact
same existing data (zero new Infrastructure/Application work):
- `Index` GET - a customer whose cart now exceeds available stock (an item
  they added earlier has since sold out or dropped below their quantity)
  is redirected to the Cart page with an explanatory error, the same
  guard-rail pattern Milestone 8.2 already used for an empty cart.
- `PlaceOrder` POST - the same check runs again immediately before final
  submission, since stock can change in the seconds between viewing Review
  and clicking Place order.

**Idempotency is a fresh, single-use `IMemoryCache` token, not a new
persistent table.** A GUID is generated every time the Review page renders
(`CheckoutReviewPageViewModel.IdempotencyKey`) and round-tripped as a hidden
form field alongside the `PlaceOrder` submission. `PlaceOrder` checks the
cache first (`IMemoryCache`, already registered for category nav caching,
15-minute TTL) - if the same key was already used successfully, it redirects
straight to the cached `Confirmation` result instead of re-running
validation, so a double-click, back-button resubmit, or network retry can
never re-validate (and potentially re-fail, e.g. if stock depleted in the
meantime) a submission that already succeeded. Deliberately **not** a real
idempotency table keyed in SQL: that shape belongs to Milestone 9.1's actual
`Order` creation, and building it now against nothing but a cached DTO would
be speculative. **Known, documented limitation**: `IMemoryCache` is
single-instance - a multi-instance deployment would need a distributed cache
or a real backing table for this to keep working; not silently glossed over,
just out of scope for a milestone with no `Order` to persist.

**`PlaceOrder` re-runs the full validation battery** - cart availability
(`GetCheckoutInputAsync`), stock sufficiency, address ownership, and
shipping-method availability (`CalculateAsync`'s `ShippingRateConfigured`
signal, same as Milestone 8.2's Review step) - and only on success caches
the result and redirects to a new `GET /Checkout/Confirmation?key=` page. A
stale/tampered `shippingMethodId` redirects back to the Shipping step,
exactly like Milestone 8.2's existing guard. `Confirmation` is explicit that
nothing has actually been placed yet ("Your order details have been
validated... nothing has been charged or shipped yet") since `Order`
entities don't exist until Milestone 9.1; a missing/expired/foreign cache
entry redirects back to `Index` with a "checkout session has expired"
message rather than erroring.

**Bug found and fixed along the way**: `Views/Cart/Index.cshtml` never
rendered `TempData["Error"]`/`TempData["Message"]` at all - it only had a
hidden, JS-controlled `<div>` for AJAX errors from the Cart page's own
in-page actions. This meant the new stock-guard redirects from Checkout
back to Cart were silently swallowed (the error was set but never
displayed) until this milestone added the same banner pattern every other
page (`Addresses`, `Checkout`) already used.

**Manually verified**: the happy path end-to-end (address -> shipping ->
review -> place order -> confirmation, with the confirmation page showing
the same real destination-based totals Review computed); resubmitting the
same idempotency key after deliberately depleting stock still replays the
original successful confirmation instead of re-validating and failing; and
a stale/tampered shipping method id on submission correctly redirects back
to the Shipping step.

## Order entities & snapshots (Milestone 9.1)

**Same brief-text gap as Milestones 6.1-8.3** - scope was agreed with the
user as a concrete, non-speculative reading of the milestone name.

**`Order`/`OrderItem` (`AuditableEntity`) mirror `PurchaseOrder`/
`PurchaseOrderItem`'s shape** (Milestone 3.3) - a status workflow that needs
`RowVersion` concurrency and an audit trail, the same reasoning that put
`PurchaseOrder` on `AuditableEntity` rather than plain `BaseEntity`. An
`Order` is created once `CheckoutController.PlaceOrder`'s existing
validation (Milestone 8.3: cart availability, stock sufficiency, address
ownership, shipping-method availability) succeeds - creating an order is a
pure "freeze this already-checked data" operation, not a second round of
validation.

**Everything the customer saw at Review is frozen onto the row, not
referenced live**:
- The shipping address is fully copied onto `Order` (`ShippingFullName`,
  `ShippingLine1`, etc.) rather than kept as an FK - `Address` (Milestone
  8.1) has no soft delete by design, so a customer deleting an address
  later must not corrupt a past order that used it.
- The applied `ShippingMethod`/`Promotion` are both snapshotted by name and
  amount (`ShippingMethodName`, `AppliedPromotionName`,
  `PromotionDiscountAmount`) even though their ids are also kept as
  `Restrict`-delete FKs - both are soft-delete-only (`AuditableEntity`), so
  the FK stays valid forever; this mirrors `Cart.AppliedPromotionId`'s
  existing `Restrict` choice exactly.
- Each `OrderItem` snapshots `ProductName`/`Sku`/`VariantDescription`/
  `ImagePath`/`UnitPrice` the same way `PurchaseOrderItem` snapshots
  `ProductName`/`ProductSku` - "an order's history stays accurate even if
  the product is later renamed, re-priced, or deactivated." `LineTotal` is
  deliberately not a stored column (`Quantity * UnitPrice` is exact and
  reproducible), matching `PurchaseOrderItem`'s own
  `UnitCost * QuantityOrdered` convention of computing rather than storing.

**`OrderStatus` deliberately has exactly one value, `Pending`, for now.**
Payment outcomes (Milestone 9.2) and the fulfillment state machine
(Milestone 10.3) each add their own states when they actually exist -
pre-adding `Paid`/`Shipped`/`Cancelled`/etc. now, with no code that could
ever set or act on them, would be speculative in exactly the way this
project avoids elsewhere.

**Stock is not reserved or deducted when an Order is created.**
`IInventoryService.ReserveStockAsync` has existed since Milestone 3.1 but
is completely unwired anywhere in the app - grepping the whole codebase
turns up only its interface, implementation, and validators. Wiring it into
order creation is explicitly Milestone 9.3's job ("Stock reservation
transaction"). The existing stock-sufficiency check (Milestone 8.3) remains
a best-effort guard only; two customers could still both successfully order
the last unit until Milestone 9.3 closes this race with a real reservation
inside the creation transaction - called out here honestly rather than
glossed over, the same as Tax/Shipping's "estimate-only" boundaries were in
Milestones 7.2/7.3.

**Idempotency upgrades from `IMemoryCache` to the real thing.** Milestone
8.3 left an explicit comment anticipating exactly this: *"a real
idempotency table once Milestone 9.1's Order exists to anchor one to."*
`Order.IdempotencyKey` (unique-indexed) now replaces the cache lookup
entirely:
- `PlaceOrder` first calls `IOrderService.GetByIdempotencyKeyAsync` - if an
  order already exists for this key (and this user), it redirects straight
  to that order's Confirmation page without re-validating anything.
- If not, it revalidates (unchanged from Milestone 8.3) and calls
  `CreateOrderAsync`, which itself re-checks for an existing order by the
  same key before inserting - closing the gap between the controller's
  check and the insert for a *sequential* duplicate.
- For a genuine *concurrent* race (two identical submissions arriving at
  the same time), the unique index on `IdempotencyKey` is the real
  safety net: `SaveChangesAsync`'s `DbUpdateException` is caught, the table
  is re-queried for the (now-existing) row created by the request that won
  the race, and that order is returned as success rather than the second
  request failing outright.
- This is durable across app restarts and multiple instances, closing the
  exact limitation Milestone 8.3 documented (`IMemoryCache` is
  single-instance) - without needing a distributed cache, since the Order
  itself is now the anchor.

**The cart is cleared on successful order placement** (`ICartService
.ClearCartAsync`, already existed since Milestone 6.1 for the "Clear cart"
button) - a real, previously-flagged gap: before this milestone, "placing
an order" left the cart's items sitting there since there was no real order
to have moved them into.

**Confirmation now reads a real order**: `GET /Checkout/Confirmation
/{orderNumber}` (`IOrderService.GetByOrderNumberAsync`, ownership-scoped
exactly like `IAddressService.GetByIdAsync` - another customer's order
number returns `NotFound`, never their data) replaces the old
`?key={idempotencyKey}` route that read from `IMemoryCache`. A missing or
foreign order number redirects back to `/Checkout` with a "we couldn't find
that order" message instead of erroring.

**Deliberately out of scope**: no "My Orders" history page (Milestone
11.1/11.2's job) and no admin order queue/detail UI (Milestone 10.1/10.2's
job) - this milestone stops at the Confirmation page a customer lands on
right after placing an order.

**Bug caught proactively this time**: `TestDatabase.ResetAsync` needed
`OrderItems`/`Orders` cleanup added *before* the tables they reference
(`Products`, `ShippingMethods`, `Promotions`) - the exact "add a new table
to this script the same milestone it's introduced" reminder already on
file from Milestones 6.3, 7.3, and 8.1 (missed each of those three times).
Caught and fixed here before it could cause the same cross-run FK-violation
test failure Milestone 7.3's report described.

**Manually verified end-to-end** against the real dev database (not just
the InMemory/test-SQL-Server suites): address -> shipping -> review ->
place order -> a real `ORD-000001`-style order number on Confirmation,
confirmed via direct SQL query that the `Orders`/`OrderItems` rows persist
the correct snapshotted address/shipping/item data - and that the cart is
genuinely empty afterward.

## Payments (Milestone 9.2)

**Same brief-text gap as Milestones 6.1-9.1** - scope was agreed with the
user as a concrete, non-speculative reading of the milestone name.

**No real payment processor account exists in this environment** - the
same reason `DevEmailSender` writes emails to a local preview file instead
of delivering them. `IPaymentGateway.ChargeAsync` (Application layer) is
backed by `SimulatedPaymentGateway` (Infrastructure, singleton-registered,
no `ApplicationDbContext` dependency - the same shape as `IFileStorage`/
`LocalFileStorage`), which "charges" a card using the well-known,
publicly-documented Stripe test-card numbers - **4242 4242 4242 4242**
always succeeds, **4000 0000 0000 0002** always declines - a real,
industry-standard convention for simulating both outcomes, not something
invented for this app. Any other card number is validated for real (Luhn
checksum, 13-19 digit length, expiry date, CVV format) and succeeds if it
passes those checks, the same leniency a sandbox gateway offers. **The real
card number is never persisted** - only a masked last-4 (`Mask`) and the
brand detected from the leading digit (`DetectBrand`) are kept, mirroring
real PCI-compliant practice even though nothing here is real.

**`Payment` deliberately does NOT derive from `AuditableEntity`** - it's
`BaseEntity` only, no soft delete, no `RowVersion` - the same reasoning
`StockMovement` (Milestone 3.1) uses: this row is written once,
synchronously, with its final outcome already known, and never updated or
deleted afterward. `ISoftDeletable`'s own doc comment is explicit that
*"immutable financial transaction records (payments, refunds, ledger
entries, audit logs) must NOT implement this interface"* - a correction
(a refund, Milestone 13.3) records a new, separate transaction rather than
editing this one. `IHasRowVersion`'s doc comment does mention "payment...
records" as needing concurrency control, but that guidance fits a mutable,
async, in-flight payment state machine (authorize -> capture -> settle) -
not this synchronous, one-shot simulation, which has nothing to protect
with optimistic concurrency since nothing ever writes to a `Payment` row a
second time.

**`OrderStatus` gains `Paid` and `PaymentFailed`** - exactly the extension
Milestone 9.1's own doc comment predicted this milestone would make.
Placing an order and charging its payment method are treated as **one
atomic step inside `OrderService.CreateOrderAsync`**, not two separate
calls a caller could invoke out of order or half-complete: the order is
inserted first (to get its `Id`/`OrderNumber`, unchanged from Milestone
9.1), then the (simulated) charge runs, then the `Payment` row and the
order's final `Status` are written together in the same final
`SaveChangesAsync`. Because the idempotency check at the top of
`CreateOrderAsync` short-circuits *before* ever calling the gateway, a
replayed submission for an already-created order (whether sequential or a
genuine concurrent race caught by the unique index on `IdempotencyKey`)
can never charge a card twice.

**A declined card does not retry in place.** The order it produced is real
and persisted (a genuine `ORD-######` number, visible on Confirmation,
marked `PaymentFailed`) - it simply isn't paid. Trying again means checking
out again from Cart (a new order, a new idempotency key, a fresh charge
attempt), not resubmitting the failed one. Building a dedicated "retry
payment on an existing order" endpoint would be more surface than this
milestone needs and arguably belongs with Milestone 10.x's order
operations instead.

**The cart is now only cleared once payment actually succeeds** - a
deliberate behavior change from Milestone 9.1, which cleared it whenever
order *creation* succeeded (before Payment existed, "created" and "paid"
were the same thing). `CheckoutController.PlaceOrder` now checks
`orderResult.Value.PaymentStatus == nameof(PaymentStatus.Succeeded)` before
calling `ICartService.ClearCartAsync` - a customer whose card was declined
keeps their cart exactly as it was, so they can immediately retry checkout
with a different card instead of re-adding everything.

**Review gained a Payment section** (card number, cardholder name, expiry
month/year, CVV) with explicit helper text naming both test-card numbers -
this is not a real payment form and should never be mistaken for one.
Confirmation shows the outcome plainly: a success banner with the masked
card/brand, or a decline banner with the gateway's `DeclineReason` and
guidance to go back and try again.

**Deliberately out of scope**: no admin payments view - surfacing payment
status alongside order status is Milestone 10.x's job as part of order
detail, not this milestone's.

**Manually verified end-to-end** against the real dev database: a
successful charge with the Stripe success test number showed `ORD-000002`
as Paid with "Visa **** **** **** 4242" on Confirmation and genuinely
cleared the cart; a declined charge with the Stripe decline test number on
a fresh cart showed `ORD-000003` as Declined with "Your card was declined."
and left the cart's item exactly in place - both confirmed via direct SQL
query against the `Orders`/`Payments` tables.

## Stock reservation transaction (Milestone 9.3)

**Same brief-text gap as Milestones 6.1-9.2** - scope was agreed with the
user as a concrete, non-speculative reading of the milestone name.

**Reuses machinery that has existed, fully built and completely unwired,
since Milestone 3.1.** `IInventoryService.ReserveStockAsync`/
`ReleaseReservationAsync` and the `InventoryReservation` entity were built
for exactly this purpose but had no caller anywhere in the app until now -
`OrderService.CreateOrderAsync` is their first real consumer. No new
Inventory-layer logic was needed.

**Reservation now runs before the payment charge, not after** - this is
the actual change of substance this milestone makes. Milestone 9.1's own
doc comment named the race this closes: *"two customers could both
successfully order the last unit"* between Milestone 8.3's best-effort
stock guard and an eventual real reservation step. Reserving first also
means a card is never charged for stock that turns out to be unavailable.

**The warehouse-selection gap, and how this milestone resolves it.**
`InventoryReservation` is keyed to a single `InventoryItemId` - one
warehouse - but nothing in Cart or Checkout has ever picked a warehouse;
`CartService`'s own stock check (`GetStockAsync`) sums `QuantityAvailable`
across every `InventoryItem` row matching a product/variant, regardless of
warehouse. `OrderService` resolves this with a documented best-fit policy:
for each order line, it loads every `InventoryItem` row for that
product/variant and reserves against whichever one currently has the most
available stock (computed in-memory, since `QuantityAvailable` is a
computed property, not a mapped column). A product/variant with no
`InventoryItem` row at all is treated as untracked/unlimited and skipped
entirely - the same leniency untracked inventory already gets on the
product detail page and Cart.

This means an order can legitimately pass the aggregate stock guard
(Milestone 8.3, and the Cart page's own availability check) and still fail
reservation, if stock for a line is split across warehouses such that no
single warehouse alone covers the requested quantity even though the sum
does. This is a known, accepted limitation - there is no warehouse-
selection UI anywhere in the app to resolve it more precisely, and building
one is out of scope for this milestone's name.

**All-or-nothing per order, via application-level compensation, not a
single enclosing transaction.** Each `InventoryService` method already
begins and commits its own transaction internally (via the codebase's
existing `BeginTransactionIfSupportedAsync` helper, duplicated identically
in `InventoryService` and `PurchaseOrderService`), so nesting a further
transaction across the `IInventoryService`/`OrderService` boundary isn't
feasible. Instead, `OrderService` tracks every reservation id it
successfully creates for the order being placed; if a later line fails, it
calls `ReleaseReservationAsync` for each already-created reservation before
recording the failure - never leaving some lines of a doomed order holding
real inventory. A genuine `DbUpdateConcurrencyException` (two orders racing
the same last unit - `InventoryItem` carries a `RowVersion` via
`AuditableEntity`) is caught and folded into the same failure path rather
than left to surface as an unhandled exception.

**New `OrderStatus.StockReservationFailed` and `Order.StockIssueMessage`.**
A reservation failure is a genuinely different outcome from `PaymentFailed`
- the remedy is different items or quantities, not a different card - so
it gets its own status value rather than reusing `PaymentFailed`.
`StockIssueMessage` (nullable, mirrors `Payment.DeclineReason`'s precedent)
records which line failed and why, so revisiting the order later (a page
reload, an idempotent replay) shows the same message consistently instead
of losing it after the first in-memory response. The order itself is still
real and persisted - `CreateOrderAsync` still returns `Result.Success`, the
same pattern Milestone 9.2 established for `PaymentFailed` - and the
payment gateway is never called at all for this outcome.

**A `PaymentFailed` order's reservations are released too.** Only a
genuinely `Paid` order keeps its reservations `Active`; holding real
inventory against an order nobody actually paid for would be wrong, and
Milestone 9.2's "trying again means a new order" precedent implies the
failed order's stock claim shouldn't persist either.

**Confirmation now branches three ways** instead of two - `Paid` (success
banner), `StockReservationFailed` (a distinct warning banner naming the
affected item and reason, explicit that the payment method was never
charged), and `PaymentFailed` (the existing decline banner, unchanged). The
Payment card is hidden entirely for `StockReservationFailed`, since no
charge was ever attempted and showing "Declined" would be misleading.

**Deliberately out of scope**: no admin reservation view; no "consume
reservation at shipment" logic - `ReservationStatus.Consumed` and
`StockMovementType.SaleCompletion` were both pre-provisioned in their enums
back in Milestone 3.1 but stay unused, reserved for Milestone 10.3's
fulfillment state machine.

**Manually verified end-to-end** against the real dev database: a product
with 3 units in each of two separate warehouses (6 available in aggregate)
correctly passed the Checkout flow's stock guard for a 5-unit line, then
correctly failed reservation once `OrderService` tried to secure all 5
units from a single best-fit warehouse - `ORD-000004` was left as
`StockReservationFailed` with the message "Not enough stock available to
reserve this quantity," zero `Payment` rows were created, both warehouses'
`QuantityReserved` remained at 0, and the cart was not cleared - all
confirmed via direct SQL query.

## Order queue UI (Milestone 10.1)

**Same brief-text gap as Milestones 6.1-9.3** - scope was agreed with the
user as a concrete, non-speculative reading of the milestone name: a
read-only, paginated admin list of every placed order, mirroring
`PurchaseOrdersController`'s Index exactly (`IOrderService.GetPagedAsync`
follows `PurchaseOrderService.GetPagedAsync`'s shape line-for-line - same
`Contains` search over two fields, same `Enum.TryParse` status filter, same
`OrderByDescending(o => o.Id)` newest-first ordering, same
`PagedResult<T>`). `OrderListItemDto` is deliberately lighter than
`OrderDto` - order number, customer name, item count, grand total, status,
placed date - the same relationship `PurchaseOrderListItemDto` has to a
full purchase order.

**No per-order detail page or actions exist yet.** Approving, shipping,
refunding, or even just viewing one order's full detail is explicitly
Milestone 10.2's job ("Order detail & operations") - this milestone stops
at the browsable list, the same restraint Milestone 4.1 showed leaving
"Best sellers" and other sections as honest placeholders pending later
milestones that actually own them.

**Two things that have existed, unused, since earlier milestones are
switched on here for the first time.** `Policies.CanManageOrders`
(`Program.cs`, roles `OrderManager`/`CustomerSupport`) has been registered
since Milestone 1's auth policy setup but had no controller using it until
`OrdersController`. The "Orders" sidebar entry in `_AdminLayout.cshtml` has
been a disabled placeholder (`<span class="nav-link disabled">`) since
Milestone 4.1's admin layout - it's now a real link.

**The "Customer" column is `Order.ShippingFullName`**, not an
`AspNetUsers` join. The order already snapshots the customer's name at
purchase time (Milestone 9.1), so there's no need to join Identity's user
table from the Application/Infrastructure order-listing path - the same
denormalized-field convention `PurchaseOrderListItemDto` uses for
`Supplier.Name` rather than re-resolving it from the `Supplier` table on
every list read.

**Manually verified** against the real dev database: all four existing
orders - including `ORD-000001`, a `Pending` order created back in
Milestone 9.1 before Milestone 9.2 introduced real payment outcomes -
rendered correctly, newest-first by id; filtering by `status=Paid` and
searching by a customer's first name (`Jane`) both correctly narrowed the
list to just the matching order.

## Order detail & operations (Milestone 10.2)

**Same brief-text gap as Milestones 6.1-10.1** - scope was agreed with the
user as a concrete, non-speculative reading of the milestone name: a full
order detail page (`IOrderService.GetByIdAsync`, deliberately not
ownership-scoped like `GetByOrderNumberAsync` - an admin can open any
customer's order) plus the one lifecycle operation genuinely available
before Milestone 10.3 builds a real fulfillment/shipment state machine.

**`CancelAsync` only accepts a `Paid` order.** A `PaymentFailed` or
`StockReservationFailed` order never held a reservation and was never
charged - there is nothing to reverse, so cancelling one is rejected with
`order.not_cancellable`. Cancelling a `Paid` order queries
`InventoryReservations` directly by `ReferenceType == "Order"` /
`ReferenceId == order.Id.ToString()` (the same reference pair
`CreateOrderAsync` writes when it reserves stock, Milestone 9.3) for any
still-`Active` rows, releases each via the existing
`IInventoryService.ReleaseReservationAsync`, then moves the order to the
new terminal `OrderStatus.Cancelled`. **Deliberately does not process a
refund** - that is Milestone 13.3's job, a separate transaction, the same
"a correction is a new transaction, not an edit to the original" precedent
`Payment`'s design already established in Milestone 9.2.

**`Order.AdminNotes`** is a free-text, staff-only field (2000-char limit,
mirrors `PurchaseOrder.Notes`'s convention) editable from the detail page
via its own `UpdateAdminNotesAsync` action - intentionally separate from
`CancelAsync` so saving a note never requires (or risks) also touching
order status. It is never rendered anywhere a customer can see it.

**The Payment card is hidden entirely for `StockReservationFailed`** on
the detail page, the same reasoning Confirmation's own outcome banner
already uses (Milestone 9.3) - no charge was ever attempted, so showing a
payment status line would be misleading rather than simply absent.

**Manually verified** against the real dev database: opened a real `Paid`
order's detail page, saved an internal note and confirmed it survived a
page reload, cancelled the order and confirmed both the `Cancelled` status
badge and the disappearance of the Cancel button on reload, and confirmed
a `PaymentFailed` order's detail page correctly shows no Cancel button at
all.

## Shipment & centralized state machine (Milestone 10.3)

**Same brief-text gap as Milestones 6.1-10.2** - scope was agreed with the
user as a concrete, non-speculative reading of the milestone name, which
names two distinct pieces of work: a real shipment record, and centralizing
what had until now been scattered ad-hoc status checks.

**`OrderStatusTransitions.CanTransition(from, to)`** (Domain layer, pure -
no `ApplicationDbContext`, no I/O) is the single definition of the legal
`OrderStatus` graph:

```
Pending  -> Paid, PaymentFailed, StockReservationFailed
Paid     -> Cancelled, Shipped
Shipped  -> Delivered
(everything else terminal)
```

Before this milestone, `CancelAsync` (Milestone 10.2) checked
`order.Status != OrderStatus.Paid` directly - a second ad-hoc check, had
Milestone 10.3 not centralized it, would have needed to appear in
`ShipAsync` too, and a third in `MarkDeliveredAsync`. All three now call
`OrderStatusTransitions.CanTransition` instead, so the legal-transition
graph exists in exactly one place - the thing "centralized state machine"
in the milestone's own name asks for. `CreateOrderAsync`'s initial
Pending-to-{Paid, PaymentFailed, StockReservationFailed} fan-out is not
routed through the same check: it originates every order at `Pending`
by construction, so the transition is always valid by definition - adding
a runtime guard there would be validating a condition that can't happen.

**Shipping consumes the reservation for good - the exact gap Milestone
3.1 pre-provisioned for.** `ReservationStatus.Consumed` and
`StockMovementType.SaleCompletion` have existed in their enums since
Milestone 3.1 but had no code path that ever produced them - every
previous consumer (`ReleaseReservationAsync`, used by Milestones 9.3 and
10.2) only ever *released* a reservation back to available stock.
`IInventoryService.ConsumeReservationAsync` is the first caller of either:
it mirrors `ReleaseReservationAsync`'s exact shape (find the active
reservation, find its `InventoryItem`, update, record a movement, commit)
but the semantics differ in one key way - `QuantityOnHand` actually
decreases (the item has physically left the warehouse), whereas
`QuantityAvailable` is unaffected by consumption (it already excluded the
reserved quantity before the shipment happened). `ShipAsync` looks up
every still-`Active` `InventoryReservation` for the order (same
`ReferenceType == "Order"` / `ReferenceId` pair `CreateOrderAsync`
originally wrote) and consumes each one.

**A `Shipment` is created, not merely a status flip.** One row per order
(a v1 scope choice mirroring `Payment`'s "one charge per order"), storing
`Carrier`/`TrackingNumber`/`ShippedAtUtc`/`DeliveredAtUtc`. It derives from
`AuditableEntity` rather than an immutable insert-once type like `Payment`
or `StockMovement`, because - like `InventoryReservation` - it has a real
two-state mutable lifecycle (shipped, then later delivered), not a single
known-at-creation outcome.

**Once `Shipped`, an order can no longer be `Cancelled`.** This isn't a
special case bolted onto `CancelAsync` - it falls straight out of the
transition table above, since `Paid` is the only state `Cancelled` is
reachable from and shipping moves the order out of `Paid`. There is no
return/refund flow yet (a later milestone's job), so a mis-shipped order
simply stays exactly as it is; the Details page's Cancel button and Ship
form both disappear once an order leaves `Paid`, replaced by "Mark
delivered" once it's `Shipped`, and by nothing at all once `Delivered`.

**Manually verified** against the real dev database: placed a fresh order
(`ORD-000005`) through the real storefront checkout, confirmed it landed
`Paid`, shipped it with a carrier and tracking number and confirmed via
direct SQL query that the reservation's `InventoryItem` row had its
`QuantityOnHand` actually decrease (not just `QuantityReserved` clear), the
reservation itself flipped to `Consumed`, and a `SaleCompletion` stock
movement was recorded - then marked it delivered and confirmed the Details
page correctly shows no further actions for a `Delivered` order.

## Customer dashboard & order list (Milestone 11.1)

**Same brief-text gap as Milestones 6.1-10.3** - scope was agreed with the
user as a concrete, non-speculative reading of the milestone name.

**`GetDashboardAsync(userId, page, pageSize)`** is deliberately its own
`IOrderService` method rather than a thin wrapper around `GetPagedAsync` -
the two serve genuinely different callers with different scoping rules.
`GetPagedAsync` (Milestone 10.1) is admin-wide and takes a `Search`/`Status`
filter; this one is hard-scoped to one `userId` (no filter - a single
customer's own order history is small enough that search/status filtering
would be premature), and additionally computes `TotalOrders`/`TotalSpent`
in the same round trip, since the "dashboard" half of the milestone name
needs them.

**`TotalSpent` is computed from the `Payment` record, not `OrderStatus`.**
`orders.Where(o => o.Payment != null && o.Payment.Status ==
PaymentStatus.Succeeded).Sum(o => o.GrandTotal)` - this correctly includes
`Paid`, `Shipped`, `Delivered`, and `Cancelled` orders (cancelling never
reverses the charge - Milestone 10.2's explicit design) and excludes
`PaymentFailed`/`StockReservationFailed`, which never resulted in a real
charge. Deriving this from the `Payment` entity directly, rather than
re-deriving an equivalent status list by hand, means it stays correct
automatically if a future milestone adds another status that still implies
a successful charge.

**No per-order detail link exists yet - this is a deliberate, direct
parallel to Milestone 10.1's own restraint.** M10.1 built the admin order
queue with no link into a single order, leaving that entirely to M10.2.
This milestone does the same on the customer side: linking each row to the
existing `/Checkout/Confirmation/{orderNumber}` page was considered and
rejected, because that page's own copy ("nothing has shipped yet") is
already stale for any order that has genuinely shipped or been delivered
since Milestone 10.3 - wiring up a link to a page with known-wrong copy
would be introducing awareness of a bug without fixing it. Building (or
fixing) the real customer order-detail page, with tracking and an invoice,
is explicitly Milestone 11.2's job.

**Routing and placement mirror `AddressesController` exactly** - a
top-level (non-Admin-area) `[Authorize]` controller at `/Orders`, account-
only with no guest concept (order history only exists once you've signed
in), linked from the Profile page's button row ("My orders") next to
"Manage addresses" - the same placement Milestone 8.1 established for
infrequent, non-badge-worthy account actions.

**Manually verified** against the real dev database: a customer with one
`Delivered` order (from Milestone 10.3's own manual verification) sees
"1" total order and "27.00" total spent on their dashboard, the order row
renders with the correct date/items/total/status, the Profile page's "My
orders" link works, and an anonymous visitor hitting `/Orders` is
redirected to login.

## Order detail, tracking, invoice (Milestone 11.2)

**Same brief-text gap as Milestones 6.1-11.1** - scope was agreed with the
user as a concrete, non-speculative reading of the milestone name, which
names three distinct pieces: a real detail page, tracking, and an invoice.

**No Application-layer changes were needed at all.** `OrdersController`'s
new `Details`/`Invoice` actions both call the same `IOrderService
.GetByOrderNumberAsync` that has existed, ownership-scoped, since Milestone
9.1 - Milestone 10.3 already extended `OrderDto` with
`Carrier`/`TrackingNumber`/`ShippedAtUtc`/`DeliveredAtUtc` when it built
`Shipment`, so this milestone's job was purely to render data that already
existed, for the first time, to the customer who owns it.

**Tracking is a status timeline, not a carrier integration.** The Details
view computes a small list of `(Label, Done, Failed)` steps from
`Model.Status` - `Placed -> Paid -> Shipped -> Delivered` for the normal
path, with `PaymentFailed`/`StockReservationFailed`/`Cancelled` each
rendered as their own short, terminal branch (mirroring Confirmation's
existing three-way split from Milestone 9.3, extended to cover the two new
Milestone 10.3 statuses). There is no real carrier API anywhere in this
app - the same "simulated, not integrated" posture `SimulatedPaymentGateway`
established in Milestone 9.2 - so "tracking" here means displaying the
`Shipment` record's own fields, not calling out to a shipping provider.

**Invoice eligibility is checked against `PaymentStatus`, not `Status`.**
`OrdersController.Invoice` redirects back to Details with a
`TempData["Error"]` unless `result.Value.PaymentStatus ==
nameof(PaymentStatus.Succeeded)`. This was deliberately not implemented as
an order-status allow-list (`Paid`/`Shipped`/`Delivered`/`Cancelled`) - a
`Cancelled` order was genuinely charged and Milestone 10.2 never refunds
it, so it still needs an invoice; checking the actual payment outcome
gets this right automatically rather than needing updating every time a
new status is added that happens to imply a successful charge (the exact
same reasoning Milestone 11.1's `TotalSpent` calculation already uses).

**The invoice is a plain HTML page with print CSS, not a generated PDF.**
No PDF-generation library exists anywhere in this solution, and introducing
one for a single print-friendly page would be a disproportionate new
dependency; a `@@media print { header, footer, .no-print { display: none;
} }` block hides the site chrome and a `window.print()` button lets the
customer print or "Print to PDF" via their own browser - the standard,
dependency-free way to produce a printable document from a web page.

**A real, self-inflicted bug fixed along the way.** Milestone 11.1's own
scope note flagged that Confirmation's banner text - "nothing has shipped
yet" - goes stale the moment a real shipment exists (Milestone 10.3),
since Confirmation is reachable again later (a bookmark, a resubmitted
idempotency key), not only immediately after checkout. Rather than
duplicating shipment rendering onto Confirmation to keep it accurate
forever, the stale claim was simply removed and a "View order details"
link was added pointing at the new Details page - the definitive, always-
current source for an order's real-time status.

**Manually verified** against the real dev database: opened a `Delivered`
order's detail page (correct four-step timeline, tracking card showing
carrier/number/dates, invoice link present), printed its invoice (correct
line items/totals/payment method, site chrome absent), confirmed a
declined order shows no invoice link and a direct hit on its Invoice URL
redirects back to Details with the expected message, and confirmed a
second customer gets a `404` attempting either the Details or Invoice URL
for an order that isn't theirs.

## Reorder (Milestone 11.3)

**Same brief-text gap as Milestones 6.1-11.2** - scope was agreed with the
user as a concrete reading of the milestone name. This closes out
Milestone 11 in its entirety (11.1 Dashboard & order list, 11.2 Order
detail/tracking/invoice, 11.3 Reorder).

**Not gated by `OrderStatusTransitions` or order status at all.** Cancel/
Ship/MarkDelivered (Milestone 10.3) all route through the centralized
state machine because they mutate the order's own lifecycle. Reorder does
not - it only reads a past order's line items and writes to the cart, so
there is no order-lifecycle invariant to protect. It is deliberately
offered on every order regardless of status, since it is arguably most
useful on a `PaymentFailed`/`StockReservationFailed` order where the
customer wants to try again with the same items.

**`ICartService.ReorderAsync` reuses `AddItemAsync` per line rather than
introducing new cart-mutation logic.** It loops the order's items, calling
the existing, already-fully-validated `AddItemAsync` (Milestone 6.1) once
per line; a per-line failure (deactivated product, invalid/inactive
variant, insufficient stock) is caught and recorded as a
`ReorderSkippedItemDto(ProductName, Reason)` instead of aborting the whole
batch, so a single stale line never blocks the rest of a - possibly
multi-item - order from being re-added. After the loop, the cart is read
once via `GetCartAsync` to return the final, fully-rebuilt state. This
mirrors `MergeGuestCartIntoUserCartAsync`'s (Milestone 6.2) precedent of
capping/skipping individual lines rather than failing an entire batch
operation over one bad line.

**The customer `OrdersController` gained `ICartService` as a dependency**
for the new `POST /Orders/{orderNumber}/Reorder` action - the first time
that controller has needed to write to the cart rather than just read
orders. `CartOwner.ForUser(UserId)` is constructed directly rather than
via `ICartOwnerAccessor` (the pattern `CartController` uses for its
guest-capable actions) - `[Authorize]` already guarantees a real signed-in
user here, so there is no guest-cart case to resolve.

**Redirects to `/Cart` and relies on the page's existing `TempData`
rendering** - `Views/Cart/Index.cshtml` has rendered
`TempData["Message"]`/`["Error"]` since Milestone 8.3's own cart bug-fix,
so no new view-level plumbing was needed to surface the outcome: an
all-succeeded reorder sets `Message`, an all-skipped reorder sets `Error`,
and a partial result sets both simultaneously (both banners render
independently, so the customer sees both what was added and what wasn't,
with why).

**Manually verified** against the real dev database: placed an order,
opened its Details page, clicked "Reorder these items", confirmed the
redirect to `/Cart` and the "Added 1 item to your cart" banner with the
correct line item and price. The deactivated-product skip path and the
cross-customer ownership block (`404`, same as Details/Invoice) are
covered by integration tests rather than repeated manually, since they
are just `AddItemAsync`'s pre-existing, already-verified validation paths
exercised through the new endpoint.

## Review submission & rating summary (Milestone 12.1)

**Same brief-text gap as Milestones 6.1-11.3** - scope was agreed with the
user as a concrete reading of the milestone name. Confirmed via research
before starting that this was a genuine clean-slate build: no `Review`/
`Rating` entity existed anywhere in the Domain layer, `ProductDetailDto`
had no rating fields, and the product page's "Reviews" tab was a static
"coming in a later milestone" paragraph.

**`Review` is `AuditableEntity`, not `BaseEntity`-only** - unlike a
`WishlistItem` bookmark (a toggle, hard-deleted on removal) a review is
substantive content that Milestone 12.2's moderation will need to
soft-delete without losing the audit trail, the same "recoverable,
auditable" reasoning every catalog/inventory/order entity already uses.
One review per `(UserId, ProductId)`, enforced via a unique index -
directly mirroring `WishlistItem`'s own uniqueness constraint.

**Any authenticated customer may review any product regardless of
purchase history - `IsVerifiedPurchase` is a badge, not a gate.**
Computed once at submission time by checking whether the reviewer has an
order containing this product whose status reflects a genuine charge
(`Paid`/`Shipped`/`Delivered`/`Cancelled` - the exact same "genuinely
charged" status set Milestone 11.1's `TotalSpent` and Milestone 11.2's
invoice eligibility already established, since a `Cancelled` order was
never refunded per Milestone 10.2). A snapshot, not a live-recomputed
flag - the same reasoning `OrderItem`'s own snapshotted fields use.

**Reviewer identity is "First name + last initial"** (e.g. "Jane D."),
not the account's full name - a privacy-conscious default judgment call,
since no other feature in this app has needed to display one customer's
identity to another and there's no prior precedent either way.

**No edit/delete, no moderation gate.** A review is visible immediately
on submission - "submission" is additive by name, and Milestone 12.2
("Moderation & abuse protection") is explicitly the very next
sub-milestone, so building a moderation queue now would be speculative
work for a feature that doesn't exist yet.

**The rating summary is computed live from the Reviews table on every
read**, not denormalized onto `Product` - matches this app's consistent
"compute at read time" posture for stock aggregation (Milestone 3.1) and
tax/shipping estimates (Milestones 7.2/7.3), and avoids a cache-
invalidation problem for a feature with no measured scale need yet. The
star breakdown (`RatingBreakdown`) always has all five keys (1-5)
present, zero-filled, so the view's bar chart never has to guard against
a missing star level.

**`ProductDetailService` now also depends on `IReviewService`** - the
exact precedent `IWishlistService` set in Milestone 6.3 for a Storefront
service enriching `ProductDetailDto` with a cross-domain, page-level
concern (`IsWishlisted` then; `RatingSummary`/`Reviews`/`HasReviewed`
now). `GetDetailAsync` gained a `reviewsPage` parameter so the Reviews
tab's list paginates independently of the rest of the page (variant
selection, related products, etc.) via its own `?reviewsPage=N` query
string param.

**The submission form is a classic MVC form POST, not AJAX** - unlike
Wishlist's toggle button, a review is substantive content worth a full
page reload and a `TempData`-surfaced outcome (`ReviewMessage`/
`ReviewError`), the same pattern Reorder (Milestone 11.3) established
for the same reason. Validation is Data Annotations on a
`ReviewFormViewModel` checked via `ModelState.IsValid`, matching
`AddressesController`'s own classic-form validation convention (not
FluentValidation - the existing `Application/Addresses/Validators`
FluentValidation validators are themselves unused by any classic-form
controller in this codebase, so introducing a matching unused one here
would just repeat that inconsistency rather than following a real
working pattern).

**Manually verified** against the real dev database: an anonymous
visitor sees "Log in to write a review" instead of the form; a signed-in
customer submits a review and it appears immediately with the updated
average rating and star-count breakdown; the same customer - having
genuinely purchased this exact product in an earlier milestone's manual
verification - sees the "Verified Purchase" badge on their own review;
and revisiting the tab shows "You've already reviewed this product" with
the form replaced accordingly.

## Moderation & abuse protection (Milestone 12.2)

**Same brief-text gap as Milestones 6.1-12.1** - scope was agreed with the
user as a concrete reading of the milestone name, building directly on the
gaps Milestone 12.1's own scope note flagged: reviews published immediately
with no report mechanism, no moderation queue, and no rate limiting. This
closes out Milestone 12 in its entirety (12.1 Review submission & rating
summary, 12.2 Moderation & abuse protection).

**`ReviewReport` is `BaseEntity`, not `AuditableEntity`** - a report is a
one-time event that's never edited after the fact, the same reasoning
`WishlistItem` uses for its own toggle records (contrast with `Review`
itself, which is `AuditableEntity` since Milestone 12.1 already anticipated
this milestone's soft-delete need). At most one report per `(Review,
reporter)`, enforced via a unique index - mirrors `Review`'s own
one-per-`(user, product)` constraint exactly.

**Acting on a review clears its reports rather than tracking a
resolved/unresolved status.** Dismiss deletes every `ReviewReport` row for
that review (it stays live); Remove soft-deletes the review itself
(`IsDeleted = true`, via the same mechanism Milestone 12.1's `Review`
entity already gets automatically from `AuditableEntity`/the global
query filter) and also clears its reports. Either way, the moderation
queue's definition - "every review with at least one report row" - stays
simple and self-maintaining, with no separate status enum to keep in
sync. There is no persistent moderation audit log in this milestone's
scope; once acted on, a review's report history is gone.

**The moderation queue reuses `Policies.CanManageOrders`** (already grants
`CustomerSupport`) rather than a new dedicated policy/role - no
"Moderator" role exists anywhere else in this app, and adding one for a
single admin screen would be speculative infrastructure with no other
consumer.

**Rate limiting mirrors Milestone 1's existing `"auth"` policy shape, but
partitions by authenticated user id instead of client IP.** Both new
policies (`reviewSubmission`, `reviewReport`) require `[Authorize]`
already, so per-account limiting is the correct unit here - IP-based
partitioning (right for pre-auth endpoints like login) would let one
abusive account escape scrutiny behind a shared/NAT'd IP, or wrongly
throttle innocent users sharing that IP with an abuser. Both policies are
config-driven with generous defaults (5 reviews/hour, 10 reports/hour) via
the same `IConfiguration`-resolved-per-request pattern the `"auth"` policy
established, including working test-time overrides through
`WebApplicationFactory` for the same reason `AuthWebApplicationFactory`
already raises `RateLimiting:AuthPermitLimit` - functional tests submit
several reviews/reports per run and would otherwise trip the limiter.

**No self-report guard.** `ReviewDto` deliberately doesn't expose the
review's owning `UserId` (Milestone 12.1's own privacy-conscious design),
so checking "is this my own review" would need new plumbing just to block
a case that's harmless anyway - a customer reporting their own review is
low-signal noise a moderator dismisses in one click, not a real abuse
vector worth the extra code.

**Manually verified** against the real dev database: one customer
reported a second customer's review; the review surfaced in the admin
moderation queue (`/Admin/Reviews`) with the correct product, reviewer,
rating, and report detail (reporter, reason, comment, timestamp);
Dismiss cleared the queue while the review remained visible on the
product page; and Remove (exercised on a second reported review) hid it
from the product page entirely via the existing soft-delete/query-filter
mechanism, with the queue returning to empty afterward.

## Cancellation (Milestone 13.1)

**Same brief-text gap as Milestones 6.1-12.2** - scope was agreed with the
user as a concrete reading of the milestone name. Milestone 10.2 already
built order cancellation, but admin-only, with no ownership check; this
milestone is its natural, non-speculative customer-facing counterpart,
following the exact Milestone 10.x (admin) -> 11.x (customer) pattern
already established for the rest of order management (dashboard, detail,
tracking, invoice, reorder).

**`CancelOwnOrderAsync` and the admin `CancelAsync` now share one private
`CancelOrderAsync(Order, ...)` helper.** Only the lookup differs -
`CancelAsync(int id)` is admin-wide (no ownership check, matching
`GetByIdAsync`'s own precedent), `CancelOwnOrderAsync(userId,
orderNumber)` is ownership-scoped (matching `GetByOrderNumberAsync`'s own
precedent: another customer's order number returns `NotFound`, never
their data). Once the order is found, both paths run through the exact
same state-machine check (`OrderStatusTransitions` - only a `Paid` order,
never one that's already shipped/delivered/cancelled/failed) and the same
"release every active reservation" loop - extracting this into a shared
helper means the cancellation rule and the reservation-release mechanics
live in exactly one place, not two copies that could drift.

**The customer Details page's "Cancel order" button appears only when
`Status == Paid`**, mirroring the admin Details page's own visibility
gate and confirm-dialog UX line for line. Still no refund - Milestone
13.3 ("Refunds & restocking") is explicitly the milestone that adds one,
the same deferral Milestone 10.2's own scope note already established.

**Manually verified** against the real dev database: placed a fresh
order, cancelled it from the customer-facing Details page, confirmed the
"Order cancelled." message and the status timeline moving to the
terminal `Cancelled` state, confirmed the Cancel button correctly
disappeared afterward, and confirmed the `Cancelled` status via a direct
SQL query against the real database.

## Returns (Milestone 13.2)

**Same brief-text gap as Milestones 6.1-13.1** - scope was agreed with the
user as a concrete reading of the milestone name, following the exact
`PurchaseOrder` request/approve/reject workflow shape already established
elsewhere in this codebase.

**`ReturnRequest`/`ReturnRequestItem` entities, gated purely by
`OrderStatus.Delivered`.** `Product.ReturnEligibility` is deliberately
unstructured free text - there is no day-count return window anywhere in
this app to enforce automatically - so eligibility is a straight order-status
check, not a date calculation. `ReturnRequestItem` extends `AuditableEntity`,
matching `OrderItem`/`PurchaseOrderItem`'s own base-type choice for a
mutable, auditable order line (unlike `GoodsReceiptItem`, which is
explicitly immutable).

**Scope is Approve/Reject only - no refund, no restock.** "Approved" means
staff have authorized the return and expect the item(s) shipped back; the
actual refund and inventory restock only happen once the item is physically
received, which is Milestone 13.3's ("Refunds & restocking") job - the same
incremental-`OrderStatus`-value pattern Milestone 10.2/10.3 already used.

**At most one open (Requested/Approved) return request per order**,
enforced at the service layer via a check-then-insert `AnyAsync` guard - the
same pattern `Review`/`ReviewReport` already use - rather than a DB-level
filtered unique index (a precedent that does exist elsewhere, e.g.
`CartConfiguration`'s `IsUnique().HasFilter(...)` on `UserId`, but staying
consistent with `Review`'s own simpler pattern was judged the better fit
here). A customer can resubmit after a rejection, since the item-quantity
check only considers the individual request being submitted.

**`OrderDto` gained a `ReturnRequests` list**, the same way `IsWishlisted`/
`RatingSummary`/`Reviews`/`HasReviewed` were bolted onto other
page-composition DTOs in Milestones 6.3/12.1. `OrderService` depends on
`IReturnService` directly - no circular dependency, since `ReturnService`
queries `Orders` via `ApplicationDbContext` directly rather than through
`IOrderService`, the same relationship `ProductDetailService` already has
with `IReviewService`.

**The customer Details page offers "Request a return" only on a `Delivered`
order with no open request already pending**, and shows a status card
(status, reason, and - if rejected - the rejection reason) once one exists.
The admin queue at `/Admin/Returns` reuses `Policies.CanManageOrders`, the
same choice the Milestone 12.2 review-moderation queue made, rather than a
new dedicated role.

**Manually verified** against the real dev database: placed a fresh order,
shipped and marked it delivered via the admin UI, submitted a return request
as the customer (saw it show as "Requested" on the Details page), approved
it from the admin queue (the queue emptied and the customer's Details page
updated to "Approved").

## Refunds & restocking (Milestone 13.3)

**Same brief-text gap as Milestones 6.1-13.2** - scope was agreed with the
user as a concrete reading of the milestone name. This is the piece
Milestone 13.2 deliberately deferred: once an approved return's item(s) are
physically received back, process the refund and put the stock back.

**A refund is a new `Refund` entity, not an edit to the original `Payment`
row.** `Order.Payment` is a single reference, not a collection, and
`Payment`'s own doc comment already anticipated this: a correction "records
a new, separate transaction rather than editing this one." `Refund`
(`Domain.Payments`, `BaseEntity`) is an immutable ledger row, the same
reasoning `Payment`/`StockMovement` already use, with a unique index on
`ReturnRequestId` as defense in depth (the real guard is the status check -
only an `Approved` request can be refunded, so a second attempt is already
rejected before a duplicate row could ever be created).

**One admin action - "Mark received & refund" - does both the refund and
the restock**, moving `ReturnRequestStatus.Approved` straight to the new
terminal `Refunded` state, rather than modeling a separate "received" state
in between. This mirrors the same minimal-states preference the rest of
this app's workflows use (e.g. `ReturnRequestStatus` itself only has as many
values as there are real decision points).

**The refund amount is the returned items' line total only** - quantity
times each `OrderItem.UnitPrice` - not a proportional share of the order's
tax or shipping. Allocating tax/shipping proportionally across a partial
return would add real complexity for a milestone whose own scope is
"reasonable conventions," so it was deliberately left out.

**Restocking targets the exact warehouse the item was originally reserved
from**, not just any warehouse with the same product. `OrderService`'s
checkout flow already records one `InventoryReservation` per order line
(Milestone 9.3), and that reservation still carries its `InventoryItemId`
even after being consumed at ship time (Milestone 10.3) - `ReturnService`
looks it up by matching the returned `OrderItem`'s product/variant against
the order's own reservations, then restocks that exact `InventoryItem`. A
product that was untracked at order time (no matching reservation) has
nothing to restock, the same leniency untracked inventory already gets
everywhere else in this app.

**New abstraction members, both following existing shapes closely:**
`IPaymentGateway.RefundAsync` (new `RefundRequest`/`RefundResult` models) -
implemented by `SimulatedPaymentGateway` to always succeed, since there is
no realistic decline scenario for reversing a charge that already
succeeded, unlike `ChargeAsync`'s Stripe-test-card decline path; and
`IInventoryService.RestockReturnedItemAsync`, the mirror image of
`ConsumeReservationAsync` - adds the quantity back to `QuantityOnHand`
(there's no reservation to touch, since the sale already completed) and
records a `CustomerReturn` `StockMovement`.

**The admin Returns page gained a second, independently paginated section**
- "Awaiting receipt" (`GetAwaitingReceiptQueueAsync`, `Approved` requests)
alongside the existing "Pending decision" section (`GetPendingQueueAsync`,
`Requested` requests) - both now implemented via one shared
`GetQueueByStatusAsync(status, ...)` helper rather than duplicated query
logic. The customer order Details page's return-status card now renders a
"Refunded" badge with the amount and date once one exists.

**Manually verified** against the real dev database: approved a return left
over from Milestone 13.2's own manual verification, marked it received from
the "Awaiting receipt" queue, and confirmed via a direct SQL query that the
`Refund` row (correct amount), the `ReturnRequestStatus.Refunded`
transition, and the exact original warehouse's restocked `QuantityOnHand`
were all correct - and that the customer's Details page reflected the
refund. This closes out Milestone 13 in full.

## Ledger & dashboard (Milestone 14.1)

**Same brief-text gap as Milestones 6.1-13.3** - scope was agreed with the
user as a concrete reading of the milestone name.

**No new "Ledger" entity.** `Payment` and `Refund` are already the
immutable ledger rows for money in/out - both their own doc comments
describe them as exactly that - so a separate financial-ledger table would
just duplicate them. `IFinanceService` composes the two instead, the same
way the existing Inventory "History" view composes `StockMovement` rows
rather than storing a separate summary table.

**`GetLedgerAsync` merges client-side, not via a SQL `UNION`.** Both
`Payment` (filtered to `Succeeded`) and `Refund` are projected into the
same `LedgerEntryDto` shape, materialized separately, then concatenated,
sorted, and paged in memory. This keeps behavior identical between the
InMemory (unit test) and SQL Server (real/integration) providers without
relying on `IQueryable.Concat` translating the same way on both - a
reasonable simplicity/correctness tradeoff for this app's data volume.
`LedgerEntryDto.Amount` is signed the way `StockMovement.QuantityChange`
already is - a charge is positive, a refund is negative - so a reader can
eyeball a running total without branching on `Type` first.

**The dashboard summary is all-time totals only** - `TotalRevenue`,
`TotalRefunded`, `NetRevenue`, `PaidOrderCount`, `RefundCount`,
`AverageOrderValue`. Date-ranged/time-series breakdowns are explicitly
Milestone 14.2's ("Cash flow") job and were deliberately left out here, the
same kind of scope boundary Milestone 13.2 drew around refunds before
Milestone 13.3 existed to own that concern.

**A pre-existing authorization gap, found and fixed while researching this
milestone.** `Policies.CanViewFinancialReports` (SuperAdmin/Admin) and
`Policies.CanProcessRefunds` (SuperAdmin/Admin/OrderManager) were both
already registered in `Program.cs` since Milestone 1, clearly planted for
this exact milestone, but neither had ever actually been referenced by an
`[Authorize]` attribute anywhere in the app - `ReturnsController.Refund`
(Milestone 13.3) used the broader class-level `CanManageOrders` policy
instead, which also grants CustomerSupport. Fixed here: `Refund` now also
carries `[Authorize(Policy = Policies.CanProcessRefunds)]` - stacked
`[Authorize]` attributes at class and method level combine with AND
semantics, so this narrows rather than replaces the class-level policy,
and `Approve`/`Reject` (customer-facing triage decisions, not money
movement) stay under `CanManageOrders` so CustomerSupport keeps that
ability.

**The admin dashboard itself stays open to every staff role** (unchanged
`[Authorize(Roles = Roles.StaffRolesCsv)]`), so it keeps working as the
front door to the whole admin area - only the financial summary cards it
now renders are gated behind `CanViewFinancialReports`, checked in
`HomeController` via `IAuthorizationService` rather than in the view, so a
role that fails the check never even triggers the aggregate queries. The
new `/Admin/Ledger` page (`LedgerController`), by contrast, is gated at
the class level - a detailed transaction listing is more sensitive than
the aggregate totals the dashboard shows.

**Manually verified** against the real dev database: the dashboard's
totals and the ledger's per-row entries both matched a direct SQL query
against `Payments`/`Refunds` exactly.

## Cash flow (Milestone 14.2)

**Same brief-text gap as Milestones 6.1-14.1** - scope was agreed with the
user as a concrete reading of the milestone name, this time after
explicitly researching the codebase first rather than assuming.

**No charting library was introduced.** The Web project has no chart/graph
NuGet package and no bundled JS charting library - Bootstrap is the only
front-end dependency anywhere in the app. Rather than add a new dependency
with zero existing precedent for a single milestone, Cash Flow stays
server-rendered like every other admin screen: a date-range-filterable
table of daily Revenue/Refunded/Net rows, with lightweight CSS-only
(`width: N%` div bars, no JS) bars for an at-a-glance read. Milestone
14.3's own name ("Reports & export") is a more natural home for richer
visualization or export if that's ever wanted.

**`GetCashFlowAsync` fills every day in the range, including zero-activity
ones.** `Payment` (Succeeded only) and `Refund` rows in `[From, To]` are
materialized and grouped by date into two dictionaries, then a day-by-day
loop from `From` to `To` looks each day up via `GetValueOrDefault` -
so gap days render as an explicit `0.00` row rather than being silently
skipped, giving a continuous timeline rather than a sparse one.

**Defaults to the 30 days ending today** when the query omits `From`/`To`,
computed from the newly-added `IClock` dependency on `FinanceService`
(previously it only needed `ApplicationDbContext`). A reversed range
(`From` after `To`) is silently swapped rather than rejected - there's no
real "invalid state" here worth surfacing as an error for an internal
admin filter form.

**Daily granularity only** - no weekly/monthly toggle. Nothing in the
codebase (doc comments, existing query patterns) hinted at needing
one, and building configurable granularity speculatively would have gone
beyond "reasonable conventions" scope.

**Gated the same way as the Ledger page** - `CanViewFinancialReports`,
inherited from `LedgerController`'s class-level `[Authorize]` since
`CashFlow` is just a second action on that controller (cash flow and the
raw ledger are the same "finance reporting" concern, just aggregated
differently, so they share a controller rather than being split into
`LedgerController` and a new single-purpose `CashFlowController`).

**Manually verified** against the real dev database: both the default
30-day view and a narrower explicit date range matched a direct SQL
`GROUP BY` query exactly, including gap days rendering as zero.

## Reports & export (Milestone 14.3)

**Same brief-text gap as Milestones 6.1-14.2** - scope was agreed with the
user as a concrete reading of the milestone name, researched first the same
way Milestone 14.2 was.

**The "Reports" nav placeholder finally goes live.** It has sat disabled in
the Accounting section of `_AdminLayout.cshtml` since Milestone 1. It's now
a real `/Admin/Reports` hub (`ReportsController.Index`) linking to Ledger,
Cash Flow, and the new Top Selling Products report - a small landing page,
not a new independent concern.

**`IReportingService` is a new, separate service from `IFinanceService`.**
`IFinanceService`'s own doc comments scope it specifically to composing
`Payment`/`Refund` - money in/out. A product-sales report isn't that kind
of concern, so rather than blur that boundary it gets its own interface
and implementation, following the same one-service-per-capability pattern
`IReturnService`/`IOrderService`/`IInventoryService` already use despite
overlapping domains.

**Top Selling Products fills a gap flagged three separate times elsewhere
in this codebase** - `CatalogBrowseModels.cs`, `IRecommendationService.cs`,
and `RecommendationService.cs` all explicitly note that a "best selling"
sort/signal was deliberately left out because "there is no order/sales
history yet to sort by." That history has existed since Milestone 9;
`GetTopSellingProductsAsync` is the first thing in this app to actually use
it, grouping `OrderItem` rows by `ProductId` (not `ProductName` - a
renamed product doesn't fragment into two rows, and the group takes its
name from the latest recorded line) for orders whose payment succeeded,
within a date range defaulting to the 30 days ending today - the same
default-range convention `GetCashFlowAsync` established in Milestone 14.2.
Materialized then grouped in-memory, the same provider-agnostic approach
`GetLedgerAsync`/`GetCashFlowAsync` already use.

**No CSV/export library was introduced**, matching the "no charting
library" call already made in Milestone 14.2 - nothing in this app
referenced one, and a hand-rolled writer (`ECommerceApp.Web.Common.CsvExport`)
that correctly quotes/escapes commas, quotes, and newlines covers every
export here, since the exported data is always simple flat rows. It backs
three new export endpoints: `LedgerController.ExportCsv` (the *entire*
ledger, unpaginated - a new `IFinanceService.GetAllLedgerEntriesAsync`
shares the same merge logic `GetLedgerAsync` already had, extracted into
a private `BuildLedgerFeedAsync` helper), `LedgerController.CashFlowExportCsv`,
and `ReportsController.TopSellingProductsExportCsv` - the latter two export
whatever date range is currently being viewed.

**Manually verified** against the real dev database: the Reports hub, the
Top Selling Products report, and all three CSV exports all matched a
direct SQL `GROUP BY` query exactly. This closes out Milestone 14 in full.

## Framework version note

The brief fixes the stack at **.NET 8**. This machine has only the **.NET 10**
SDK/runtime installed (confirmed by probing `dotnet --list-runtimes` and a
throwaway `net8.0` project, which failed to resolve `WebApplication` because
the net8.0 ASP.NET Core reference pack isn't present). Per the project owner's
decision, the entire solution targets **`net10.0`** instead, with EF Core,
ASP.NET Core Identity, and all other packages pinned to their net10.0-
compatible versions. No other architectural or scope decision in the brief is
affected by this substitution.

## Cross-cutting concerns

- **Auditing & soft delete**: entities derive from `AuditableEntity`
  (`Id`, `CreatedAtUtc`, `CreatedByUserId`, `UpdatedAtUtc`, `UpdatedByUserId`,
  `IsDeleted`, `RowVersion`). `ApplicationDbContext.SaveChanges[Async]`
  stamps the audit fields from `IClock`/`ICurrentUserService` and converts
  deletes of `ISoftDeletable` entities into an `IsDeleted = true` update.
  A global EF Core query filter hides soft-deleted rows automatically.
  Immutable financial/audit records must NOT implement `ISoftDeletable`.
- **Errors**: application code returns `Result`/`Result<T>` for expected
  failure paths instead of throwing. `Error` carries a machine-readable
  `ErrorType` (Validation, NotFound, Conflict, Unauthorized, Forbidden,
  Unexpected) that the Web layer maps to the right HTTP status.
- **Exception handling**: unhandled exceptions on `/api/*` are caught by
  `ApiExceptionHandler` and returned as RFC 7807 `ProblemDetails`; everything
  else falls back to the branded `/Home/Error` view. No exception details are
  ever shown to the user.
- **Correlation IDs**: `CorrelationIdMiddleware` assigns/propagates an
  `X-Correlation-Id` header and pushes it into the Serilog log context so all
  log lines for one request can be tied together.
- **Health checks**: `/health/live` reports process liveness only (no
  dependency checks). `/health/ready` additionally checks SQL Server
  reachability via `SqlServerHealthCheck`.

## Configuration resolution timing

Any configuration read that must reflect `WebApplicationFactory` test
overrides (connection strings, JWT settings, rate-limit thresholds) has to be
resolved **inside** a lazily-invoked delegate (an `AddDbContext`/`AddJwtBearer`
options callback, or a per-request rate-limiter partition function) rather
than captured into a local variable before `builder.Build()`. Test overrides
only merge into the final `IConfiguration` at `Build()` time, so an eager
read silently sees only the pre-test configuration. See `Security.md` for the
three places this actually broke Milestone 1's integration tests before being
fixed.

## Solution layout

See the root `README.md` for the full directory tree and setup instructions.
