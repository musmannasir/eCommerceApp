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
