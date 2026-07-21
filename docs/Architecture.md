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
