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
| M7.1 - Promotions & coupons | **Complete - scope note** | Same as M6.1-M6.3: no brief text was available this session, so scope was agreed as reasonable conventions. `Promotion` (`AuditableEntity`) is either automatic (`CouponCode` null) or code-based, with `PromotionDiscountType` (Percentage/FixedAmount), `PromotionScopeType` (EntireOrder/Category/Brand/Product - only the matching scope FK is set), `MinimumOrderAmount`, `MaxDiscountAmount` (caps a percentage discount, and no discount type can ever exceed what it's discounting), a start/end date window, and `IsActive`. **Only code-based promotions are reachable this milestone** - `IPromotionService.FindApplicablePromotionAsync` looks a promotion up by coupon code (case-insensitive), so an automatic promotion (no code) is admin-creatable for completeness but never auto-applied to a cart; there's no design yet for resolving precedence among several automatic promotions at once under the "no stacking" v1 rule, so that wiring is deferred. **`MaxTotalUses`/`MaxUsesPerCustomer` are schema fields only, not enforced** - there's no reliable "this purchase actually completed" signal to count against until `Order` entities exist (Milestone 9); enforcing at cart-apply time alone would let an abandoned cart consume a limited-use code. `Cart.AppliedPromotionId` holds at most one applied promotion at a time (no stacking); it's re-validated against the cart's current contents on every read (`CartService.BuildCartDtoAsync`), not just at apply-time, and silently cleared (same pattern as an unavailable cart item) if it's become invalid - expired, deactivated, minimum order no longer met, or its scoped category/brand/product no longer present in the cart. `CartDto` gained `AppliedCouponCode`/`AppliedPromotionName`/`PromotionDiscount`/`Total` (`Subtotal` keeps its existing pre-discount meaning). Admin CRUD (`PromotionsController`) mirrors `HomePageBannersController` exactly and reuses `Policies.CanManageCatalog` (same as HomePageBanners - no separate Marketing policy exists). Customer-facing coupon entry is a small AJAX apply/remove on the Cart page, same CSRF-header pattern as every other Cart endpoint. |
| M7.2 - Tax service | **Complete - scope note** | Same as M6.1-M7.1: no brief text was available this session, so scope was agreed as reasonable conventions. `TaxRate` (`AuditableEntity`) maps `(CountryCode, RegionCode?, TaxCategory)` to a percentage - `RegionCode` null means a whole-country rate, with a region-specific rate taking precedence over the country-wide one for the same category when both exist (two filtered unique indexes, same technique as Carts' `UserId`/`GuestToken` pair). `TaxCategory` matches `Product.TaxCategory` by plain case-insensitive string equality, not a shared FK/enum - both stay free-text per the Data-Dictionary's pre-existing note that a structured tax-category model doesn't exist yet; a typo in either place silently yields no match, a known limitation, not a defect. **No real customer destination exists yet** - `Address` doesn't arrive until Milestone 8.1, and the Checkout Calculation Service (Milestone 7.4) is what will actually combine Tax + Shipping + Promotion into a final order total against a real address. So this milestone's only wired consumer is an **"Estimated tax"** line on the Cart page, computed against the store's configured default jurisdiction (`Store:DefaultTaxCountryCode`/`Store:DefaultTaxRegionCode`, same convention as the pre-existing `Store:PricesIncludeTax` flag) rather than a real destination, and against **pre-discount** line totals - allocating a cart-level Promotion discount across lines for tax purposes is deferred to the Checkout Calculation Service, not this estimate. `CartDto` gained `EstimatedTax`/`EstimatedTaxRateConfigured` (the latter distinguishes "no rate configured for this jurisdiction" from a genuine 0% rate, so the Cart page only shows the line once something's actually been set up); a non-taxable product (`Product.IsTaxable = false`) is excluded from the estimate, and lines with different `TaxCategory` values are taxed and summed individually. `ITaxService.CalculateTaxAsync(amount, category, countryCode, regionCode)` is destination-agnostic and ready for Milestone 8's real checkout to call with an actual address; `CalculateEstimatedTaxAsync(lines)` is the config-driven convenience wrapper Cart uses today. Admin CRUD (`TaxRatesController`) mirrors `PromotionsController`'s shape and reuses `Policies.CanManageCatalog` (no separate Checkout/Finance policy exists yet); its nav entry introduces a new "Checkout" sidebar section, a forward-compatible home for Milestone 7.3's Shipping Rates admin UI. **Bug found and fixed post-milestone (during M8.3's manual verification)**: `RateConflictsAsync` only checked non-deleted rows, but the unique indexes enforcing `(CountryCode, RegionCode, TaxCategory)` have no `IsDeleted` filter, so recreating a rate matching a previously-deleted one passed the app's check and then threw an unhandled `DbUpdateException` at save time. Fixed by having the conflict check use `IgnoreQueryFilters()` and return a message pointing at the Deleted list when the match is a soft-deleted row - see `Architecture.md`. |
| M7.3 - Shipping | **Complete - scope note** | Same as M6.1-M7.2: no brief text was available this session, so scope was agreed as reasonable conventions. `ShippingMethod` (`AuditableEntity`) is a named, admin-managed method (e.g. "Standard", "Express") for a jurisdiction - `RegionCode` null means whole-country. Unlike `TaxRate` (one rate per category per jurisdiction), several named methods can coexist for the same jurisdiction, so uniqueness is on `Name` within the jurisdiction rather than the jurisdiction alone (still the same dual-filtered-index technique for the nullable `RegionCode`). Cost is `BaseRate + RatePerKg * totalOrderWeight`, using `Product.Weight` - a field that's existed unused since Milestone 2.4 - waived entirely once an optional `FreeShippingThreshold` is met against the (pre-discount) subtotal; a line whose product has no recorded weight contributes 0kg, the same leniency untracked inventory already gets. **Same estimate-only scope boundary as Tax**: no real destination exists until Milestone 8.1's Addresses, and no method-picker UI exists until Milestone 8.2, so `IShippingService` splits into `GetAvailableShippingOptionsAsync(weight, subtotal, countryCode, regionCode)` (destination-explicit, returns every active method for a jurisdiction with its computed cost, ready for both once they exist) and `CalculateEstimatedShippingAsync(weight, subtotal)` (the store-default-jurisdiction convenience wrapper, `Store:DefaultShippingCountryCode`/`Store:DefaultShippingRegionCode`, that actually powers the Cart page's **"Estimated shipping"** line today - showing the cheapest available option, since there's no picker yet). `CartDto` gained `EstimatedShipping`/`EstimatedShippingRateConfigured` (the latter distinguishes "nothing configured" from a genuine free/zero-cost method). Admin CRUD (`ShippingMethodsController`) mirrors `TaxRatesController`'s shape, reuses `Policies.CanManageCatalog`, and sits in the "Checkout" nav section next to Tax Rates. **Bug found and fixed during this milestone's own testing**: `TestDatabase.ResetAsync` (the integration test suite's per-collection database reset) had never been updated to clear `Promotions`/`TaxRates`/`ShippingMethods` when those tables were introduced in M7.1/M7.2/M7.3 - the exact "a new table must be added to this script the same milestone it's introduced" reminder already on file from Milestone 6.3. Accumulated cross-run data caused a `ShippingMethod` integration test to see a leftover method from a prior run and fail; fixed by adding all three tables' cleanup (and identity reseeds) to the script. A planned "no method configured" integration test was dropped instead of chased further - unlike Tax's lookup (which includes a `TaxCategory` dimension each test can randomize for isolation), Shipping's lookup key is just `(CountryCode, RegionCode)`, and the real app's fixed default jurisdiction has no equivalent randomizable dimension; the same logic is already fully covered by isolated unit tests (InMemory DB, fresh per test). **Second bug found and fixed post-milestone (during M8.3's manual verification)**: the same soft-delete-vs-unique-index mismatch as Tax's fix above - `NameConflictsAsync` didn't see soft-deleted rows, so recreating a method matching a previously-deleted name+jurisdiction threw the same unhandled `DbUpdateException`. Same fix applied. |
| M7.4 - Checkout calculation service | **Complete - scope note** | Same as M6.1-M7.3: no brief text was available this session, so scope was agreed as reasonable conventions. `ICheckoutCalculationService` composes `IPromotionService`/`ITaxService`/`IShippingService` into one final total, closing the exact gap M7.2/M7.3 each explicitly deferred: their "estimated tax"/"estimated shipping" figures were computed against the **pre-discount** amount. `PromotionApplicationDto` gained `LineDiscounts` (one entry per cart line, summing exactly to `DiscountAmount` via a rounding-safe proportional allocation, computed inside `PromotionService.Evaluate`), letting the Checkout Calculation Service know exactly how much of a cart-level discount applies to each line before computing tax/shipping against the remainder. **Same estimate-only scope boundary as Tax/Shipping**: no real destination or method-picker UI exists until Milestone 8, so `ICheckoutCalculationService` splits into `CalculateAsync` (destination-explicit, sums per-line tax via `ITaxService.CalculateTaxAsync` and picks the cheapest `IShippingService` option unless one is explicitly selected - ready for Milestone 8.2, no real consumer yet) and `CalculateEstimatedAsync` (the store-default-jurisdiction convenience wrapper that now powers the Cart page, reusing `ITaxService`/`IShippingService`'s existing estimate methods by simply passing post-discount amounts into them). `CartDto` gained `EstimatedGrandTotal` (`Total + EstimatedTax + EstimatedShipping`); `CartService.BuildCartDtoAsync` now calls `ICheckoutCalculationService.CalculateEstimatedAsync` instead of `ITaxService`/`IShippingService` directly. Never fails - an invalid/missing promotion is treated as "no discount," the same as a pure calculator with no side effects (mirroring `IPricingService`); the caller remains responsible for actually clearing an invalid promotion from persisted state. Manually verified end-to-end: applying a coupon that drops the cart's subtotal below a configured free-shipping threshold now correctly starts charging shipping instead of incorrectly staying free, and estimated tax drops to reflect the discounted line amount - exactly the bug the pre-M7.4 architecture had. |
| M8.1 - Addresses | **Complete - scope note** | Same as M6.1-M7.4: no brief text was available this session, so scope was agreed as reasonable conventions. `Address` (plain `BaseEntity`, no soft delete/RowVersion - the same customer-owned-personal-data convention `Cart`/`CartItem`/`WishlistItem` already established, since a user who deletes their own address wants it gone and there's no admin recycle bin for personal data) is a single, unified address book per user - not split into separate shipping/billing entity types, since a v1 address book doesn't need that distinction; the customer just picks one at checkout (Milestone 8.2). `CountryCode`/`RegionCode` deliberately mirror `TaxRate`/`ShippingMethod`'s shape exactly, so a real address can be passed straight into `ICheckoutCalculationService.CalculateAsync` (Milestone 7.4) once real checkout exists, with no adapter needed. `IsDefault` (at most one per user) is enforced by `AddressService`, not a DB constraint - a customer's very first address is always the default regardless of the request's flag, since leaving them with zero default addresses right after saving their first one would be surprising; deleting the current default leaves no default at all rather than silently promoting another one. Account-only, like Wishlist - every `IAddressService` method scopes its query by `UserId`, and an id belonging to a different user returns `NotFound` (never `Forbidden`), so existence isn't leaked across accounts - verified over real HTTP, not just in-process. Classic server-rendered forms (`AddressesController`/`Views/Addresses`), not AJAX - an address has many fields, unlike Cart/Wishlist's single-value toggle actions - mirroring `AccountController.ChangePassword`'s pattern instead; linked from the Profile page ("Manage addresses"), not the header nav, since it's an infrequent action unlike Cart/Wishlist's badge-worthy frequency. **Same "add a new table to `TestDatabase.ResetAsync`" reminder from M6.3/M7.3, caught proactively this time**: `Address.UserId` has no DB-level FK to `AspNetUsers` (Domain can't reference Infrastructure's `ApplicationUser`), so deleting a test user would NOT cascade-delete their addresses the way `WishlistItem`'s FK-to-Product cascade incidentally does - added explicit cleanup before it could cause a cross-run collision. |
| M8.2 - Checkout flow UI | **Complete - scope note** | Same as M6.1-M8.1: no brief text was available this session, so scope was agreed as reasonable conventions. **Order placement itself is out of scope** - `Order` entities don't exist until Milestone 9.1 - so this milestone builds the real checkout *flow* (address selection -> shipping method selection -> final review with real destination-based totals) but Review's "Place order" button stays disabled with a placeholder tooltip, the same pattern the Cart page's own Checkout button used since Milestone 6.1 until this milestone replaced it with a real link into this flow. A stateless, three-step flow (`CheckoutController`) carried entirely via query string (`addressId`, then `addressId`+`shippingMethodId`) - no session/wizard state needed, since every step re-derives what it needs from the cart, the address book, and the shipping method list. **Account-only** ([Authorize]) since Address (Milestone 8.1) has no guest concept - a guest with items in their cart is redirected to log in like any other `[Authorize]` page, and their guest cart already merges into their account on login (Milestone 6.2), so nothing is lost. **One address serves both the tax and shipping jurisdiction** for `ICheckoutCalculationService.CalculateAsync` (Milestone 7.4) - consistent with Address's "no shipping/billing split" decision - so this milestone is the calculation service's first real destination-explicit consumer; its estimate-only convenience wrapper (`CalculateEstimatedAsync`) remains what the Cart page uses. `ICartService` gained `GetCheckoutInputAsync` (the cart's currently-available lines plus the resolved applied promotion id, in the shape the calculation service needs), extracted from `CartService`'s existing per-line-building logic via a new shared `ComputeAvailableLines` helper rather than duplicating it a third time. Guard rails: an empty cart redirects to the Cart page; a customer with items but no saved address redirects to `Addresses/Create` (which gained `returnUrl` support so they land back in Checkout after saving); an address/shipping-method id that doesn't belong to the current customer (or is stale after the cart/address changed) redirects back to the appropriate earlier step rather than erroring, reusing `CalculateAsync`'s existing `ShippingRateConfigured=false` signal for "the selected method doesn't match any option available for this jurisdiction" - both a tampered id and "nothing configured at all" converge on the same, already-informative Shipping page. Manually verified end-to-end (address -> shipping -> review, including a coupon discount correctly reducing both the taxed amount and the final total) against a real destination, distinct from the Cart page's store-default-jurisdiction estimate. |
| M8.3 - Server-side revalidation & idempotency | **Complete - scope note** | Same as M6.1-M8.2: no brief text was available this session, so scope was agreed as reasonable conventions given this milestone sits before `Order` entities exist (Milestone 9.1). **Server-side revalidation**: the app's "always compute fresh from live data" design already inherently revalidates price/promotion/address/shipping on every read, so the only real gap was that **stock sufficiency** was never checked in the Checkout flow itself (only informationally flagged on the Cart page via `CartItemDto.QuantityExceedsStock`, since Milestone 6.2) - `CheckoutController` now guards against it at flow entry (`Index` GET) and at final submission (`PlaceOrder` POST), reusing that same existing data with zero new Infrastructure/Application work. **Idempotency**: a fresh GUID token is generated every time the Review page renders and submitted as a hidden field alongside `PlaceOrder`; it's tracked via `IMemoryCache` (already registered for category nav caching) with a 15-minute TTL - deliberately not a new persistent table, since that would be premature ahead of Milestone 9.1's real order-creation needs. A duplicate submission (double-click, back button, network retry) with the same key replays the already-validated cached outcome instead of re-running checks that could now fail differently (e.g. stock depleted between Review and a retried submit) - verified manually by resubmitting the same key after deliberately depleting stock and confirming the original success still replays. Known, documented limitation: `IMemoryCache` is single-instance and wouldn't survive a multi-instance deployment without a distributed cache or a real idempotency table. `PlaceOrder` re-runs the full validation battery (cart availability, stock, address ownership, shipping availability) and, on success, caches the result keyed by the idempotency token before redirecting to a new `GET /Checkout/Confirmation?key=` page - explicit that "your order details have been validated" is not a real placed order, since `Order` entities don't exist until Milestone 9.1. A pre-existing gap found and fixed along the way: `Views/Cart/Index.cshtml` never rendered `TempData["Error"]`/`TempData["Message"]` at all (only had a hidden, JS-controlled AJAX error div), so this milestone's new stock-guard redirects back to Cart were silently swallowed until the banner was added. |
| M9.1 - Order entities & snapshots | **Complete - scope note** | Same as M6.1-M8.3: no brief text was available this session, so scope was agreed as reasonable conventions. `Order`/`OrderItem` (`AuditableEntity`, mirroring `PurchaseOrder`/`PurchaseOrderItem`'s shape) are created once `CheckoutController.PlaceOrder`'s existing validation (Milestone 8.3) succeeds - everything the customer saw at Review is frozen onto the row rather than referenced live: the shipping address is fully copied (`Address` has no soft delete, so a customer deleting it later must not corrupt past orders), and the applied shipping method/promotion are both snapshotted by name/amount even though their ids are also kept (`Restrict`-delete FKs, mirroring `Cart.AppliedPromotionId`'s choice, since both are soft-delete-only). `OrderStatus` deliberately has just one value, `Pending`, for now - Payment outcomes (M9.2) and the fulfillment state machine (M10.3) each add their own states when they exist; adding them now would be speculative. **Stock is not reserved or deducted when an order is created** - `IInventoryService.ReserveStockAsync` exists (Milestone 3.1) but is completely unwired anywhere in the app, and that wiring is explicitly Milestone 9.3's job ("Stock reservation transaction"); the existing stock-sufficiency check (Milestone 8.3) remains a best-effort guard only, and the race it doesn't close is called out honestly rather than glossed over. **Idempotency upgrades from `IMemoryCache` to the real thing**: Milestone 8.3 left an explicit comment anticipating this - "a real idempotency table once Milestone 9.1's Order exists to anchor one to" - so `Order.IdempotencyKey` (unique-indexed) now replaces the cache lookup entirely; a duplicate submission durably replays the same order even across app restarts, and a genuine race between two identical submissions is caught by the database's unique constraint (caught `DbUpdateException`, re-queried, existing order returned) rather than a check-then-act gap. **The cart is now cleared on successful order placement** - a real, previously-flagged gap (it silently wasn't before, since there was no real order to place). No "My Orders" history page or admin order queue - those are Milestone 11.x's and 10.x's jobs respectively; this milestone stops at a Confirmation page that reads the real, persisted order by order number (`GET /Checkout/Confirmation/{orderNumber}`, ownership-scoped like `IAddressService`). **A real, pre-existing gap caught and fixed proactively this time**: `TestDatabase.ResetAsync` needed `OrderItems`/`Orders` cleanup added before the tables they reference (`Products`, `ShippingMethods`, `Promotions`) - the same "add a new table to this script the same milestone it's introduced" reminder already on file from Milestone 6.3/7.3/8.1, caught before it could cause a cross-run FK-violation failure like M7.3's did. |
| M9.2 - Payments | **Complete - scope note** | Same as M6.1-M9.1: no brief text was available this session, so scope was agreed as reasonable conventions. No real payment processor account exists in this environment, so `IPaymentGateway` (mirroring `IFileStorage`/`IEmailSender`'s local/simulated-implementation pattern) is backed by `SimulatedPaymentGateway`, which "charges" a card using the well-known, publicly documented Stripe test-card numbers (4242 4242 4242 4242 always succeeds, 4000 0000 0000 0002 always declines) plus real Luhn/length/expiry/CVV format checks - the real card number is never persisted, only a masked last-4 and detected brand, mirroring PCI-compliant practice even in simulation. `Payment` (`BaseEntity`, no soft delete/RowVersion) mirrors `StockMovement`'s "immutable, insert-once ledger entry" reasoning rather than `AuditableEntity`'s - a charge attempt's outcome is known synchronously and never updated afterward; a correction (refund) is Milestone 13.3's job and would record a separate transaction. `OrderStatus` gains `Paid`/`PaymentFailed` (exactly the extension Milestone 9.1's doc comment predicted this milestone would make), set as part of the same `CreateOrderAsync` call that creates the order - placing an order and charging its payment method are one atomic step, not two a caller could invoke out of order. A declined card still leaves a real, persisted order (visible on Confirmation, marked `PaymentFailed`) rather than silently failing - it does not retry in place; trying again means checking out again (a new order, a new idempotency key). **The cart is now only cleared once payment actually succeeds** - a deliberate behavior change from Milestone 9.1 (which cleared it on any successful order *creation*), so a customer whose card was declined can immediately retry checkout with the same cart contents instead of having to re-add everything. No admin payments view - surfacing payment status is Milestone 10.x's job as part of order detail. Review's payment form and Confirmation's outcome banner were manually verified end-to-end against the real dev database for both outcomes - a successful charge (cart cleared, `ORD-000002` shown as Paid with "Visa **** **** **** 4242") and a declined charge (cart retained, `ORD-000003` shown as Declined with the decline reason) - with the `Orders`/`Payments` rows confirmed via direct SQL query. |
| M9.3 - Stock reservation transaction | **Complete - scope note** | Same as M6.1-M9.2: no brief text was available this session, so scope was agreed as reasonable conventions. Reuses the already-built, previously completely unwired `IInventoryService.ReserveStockAsync`/`ReleaseReservationAsync` (Milestone 3.1) - `OrderService.CreateOrderAsync` becomes their first real caller, with no new Inventory-layer logic needed. **Reservation now happens before the payment charge, not after** - closing the exact race Milestone 9.1's own doc comments flagged as open ("two customers could both successfully order the last unit"), and never charging a card for stock that turns out unavailable. **Warehouse-selection gap, resolved with a documented best-fit policy**: `InventoryReservation` is keyed to a single `InventoryItemId` (one warehouse), but nothing in Cart/Checkout has ever picked a warehouse - `CartService`'s own stock check sums availability across every warehouse. For each order line, `OrderService` now looks up every `InventoryItem` row for that product/variant across all warehouses and reserves against whichever has the most available stock; a product with no `InventoryItem` row at all stays untracked/unlimited (no reservation attempted), the same leniency untracked inventory already gets elsewhere. This means an order can pass the aggregate stock guard (Milestone 8.3) but still fail reservation if the one best-fit warehouse alone can't cover the line - a known, accepted limitation until a real warehouse-selection UI exists. **All-or-nothing per order**: if any line's reservation fails, every reservation already created for that same order is released via application-level compensation (`ReleaseReservationAsync` per id) rather than a single enclosing DB transaction, since each `InventoryService` method already begins and commits its own transaction internally; a genuine `DbUpdateConcurrencyException` (two orders racing the same last unit) is caught and treated as an ordinary reservation failure rather than an unhandled exception. New `OrderStatus.StockReservationFailed` (a genuinely different outcome from `PaymentFailed` - the remedy is different items/quantities, not a different card) and `Order.StockIssueMessage` (mirrors `Payment.DeclineReason`'s precedent) record which line failed and why; the order is still real and persisted, `CreateOrderAsync` still returns `Result.Success`, and the card is never charged. A `PaymentFailed` order's reservations are released too - only a genuinely `Paid` order keeps them `Active`. Confirmation now branches three ways (Paid/PaymentFailed/StockReservationFailed) instead of two, and hides the Payment card entirely when no charge was ever attempted. Out of scope: no admin reservation view, no "consume reservation at shipment" logic (`ReservationStatus.Consumed`/`StockMovementType.SaleCompletion` stay unused, reserved for Milestone 10.3's fulfillment state machine). Manually verified end-to-end against the real dev database: a product split 3+3 across two warehouses correctly passed the aggregate guard for a 5-unit line, then correctly failed reservation at the best-fit warehouse (`ORD-000004`, "Not enough stock available to reserve this quantity"), left zero `Payment` rows, left both warehouses' `QuantityReserved` at 0, and did not clear the cart. |
| M10.1 - Order queue UI | **Complete - scope note** | Same as M6.1-M9.3: no brief text was available this session, so scope was agreed as reasonable conventions. A read-only, paginated admin order queue (`GET /Admin/Orders`) - every placed order, searchable by order number or customer name, filterable by status, newest-first - mirroring `PurchaseOrdersController`'s Index exactly (same query-string paging pattern, same `PagedResult<T>` shape). Deliberately **no per-order detail page or actions** (approve, ship, refund, etc.) - that's explicitly Milestone 10.2's job ("Order detail & operations"); this milestone is the browsable list only. Switches on two things that have existed, unused, since earlier milestones: `Policies.CanManageOrders` (registered in `Program.cs` since Milestone 1, roles `OrderManager`/`CustomerSupport`, never assigned to a controller until now) and the disabled "Orders" sidebar link (a placeholder since Milestone 4.1's admin layout). The queue's "Customer" column uses `Order.ShippingFullName` (already snapshotted on the order) rather than joining Identity's `AspNetUsers` - the same denormalized-field convention `PurchaseOrderListItemDto` uses for `Supplier.Name`. Manually verified against the real dev database: all four existing orders (including a `Pending` one created before Milestone 9.2 introduced payment outcomes) render correctly newest-first, and both the search box and status filter correctly narrow the list. |
| M10.2 - Order detail & operations | **Complete - scope note** | Same as M6.1-M10.1: no brief text was available this session, so scope was agreed as reasonable conventions. Admin order detail page (`GET /Admin/Orders/Details/{id}`, admin-scoped via new `IOrderService.GetByIdAsync` - no per-customer ownership check, unlike the customer-facing `GetByOrderNumberAsync`) shows everything: shipping address, shipping method, payment outcome (masked card/decline reason, hidden entirely for `StockReservationFailed` since no charge was ever attempted), stock-issue message, line items, and totals. **Cancel operation**: the one order-lifecycle action available before Milestone 10.3 builds a real fulfillment/shipment state machine - cancelling a `Paid` order releases its active stock reservations and moves it to a new terminal `OrderStatus.Cancelled`, but does **not** process a refund (Milestone 13.3's job, a separate transaction, the same precedent Payment's design already established). Only offered on `Paid` orders - a `PaymentFailed`/`StockReservationFailed` order never held a reservation or a charge, so there's nothing to cancel. **Internal admin notes**: a free-text, staff-only `Order.AdminNotes` field (mirrors `PurchaseOrder.Notes`'s 2000-char convention), editable from the detail page, never shown to the customer. The Index queue (Milestone 10.1) gains the "Open" link it deliberately omitted. Manually verified against the real dev database: viewed a `Paid` order's full detail, saved an internal note and confirmed it persisted on reload, cancelled the order (confirmed `Cancelled` status and the Cancel button disappearing), and confirmed a `PaymentFailed` order correctly shows no Cancel button. |
| M10.3 - Shipment & centralized state machine | **Complete - scope note** | Same as M6.1-M10.2: no brief text was available this session, so scope was agreed as reasonable conventions. `OrderStatus` gains `Shipped`/`Delivered`; a new `Shipment` entity (one per order - `Carrier`, `TrackingNumber`, `ShippedAtUtc`, `DeliveredAtUtc?`) records the fulfillment event. **Centralized state machine**: a new pure, no-I/O `OrderStatusTransitions.CanTransition(from, to)` (Domain layer) is now the single definition of every legal order status change (`Pending→{Paid, PaymentFailed, StockReservationFailed}`, `Paid→{Cancelled, Shipped}`, `Shipped→{Delivered}`, everything else terminal) - `CancelAsync`, `ShipAsync`, and `MarkDeliveredAsync` all route through it instead of each checking its own ad-hoc condition, closing the exact gap the milestone name calls out. **Shipping finally consumes the stock reservation for good** - closing the loop `ReservationStatus.Consumed`/`StockMovementType.SaleCompletion` were pre-provisioned for back in Milestone 3.1 but left completely unused through Milestones 9.1-10.2: a new `IInventoryService.ConsumeReservationAsync` permanently deducts on-hand quantity (mirroring `ReleaseReservationAsync`'s shape, but the stock has physically left the warehouse rather than becoming available again). Shipping requires a carrier and tracking number; marking delivered is a simple one-way follow-up. **Once shipped, an order can no longer be cancelled** - there is no return/refund flow yet, so a mis-shipped order simply stays as it is; this "Cancel disappears once Shipped" behavior falls straight out of the same centralized transition table, not a separate check. Manually verified end-to-end against the real dev database: placed a fresh order (`ORD-000005`, Paid), shipped it with a carrier/tracking number (confirmed the reservation was consumed - on-hand quantity decreased, reservation status `Consumed`, a `SaleCompletion` stock movement recorded, and the Cancel button disappeared from the detail page), then marked it delivered (confirmed the terminal state with no further actions offered). |
| M11.1 - Dashboard & order list | **Complete - scope note** | Same as M6.1-M10.3: no brief text was available this session, so scope was agreed as reasonable conventions. Customer-facing "My Orders" page (`GET /Orders`, account-only like Addresses/Wishlist - no guest concept) - ownership-scoped to the signed-in customer, unlike Milestone 10.1's admin-wide queue. The "dashboard" is a small summary atop the paginated list: total order count and total spent, the latter computed from `Payment.Status == Succeeded` (covers Paid/Shipped/Delivered/Cancelled, since cancelling never reverses the charge - Milestone 10.2 - and excludes PaymentFailed/StockReservationFailed, which were never actually charged). **Deliberately has no per-order detail link yet** - mirrors Milestone 10.1's own precedent exactly (it built the admin queue with no detail link, deferring to 10.2): a dedicated customer order-detail page with tracking/invoice is explicitly Milestone 11.2's job, and linking rows to the existing Checkout Confirmation page instead would drag in a real bug (its "nothing has shipped yet" copy is wrong once a real shipment exists) that's more honestly 11.2's to fix alongside building the real page. Linked from the Profile page ("My orders"), the same placement precedent Addresses (Milestone 8.1) established for infrequent, non-badge-worthy account actions. Manually verified against the real dev database: a customer with one delivered order sees "1" total order and the correct total spent ($27.00) on their dashboard, the order list row renders correctly, the Profile page link works, and an anonymous visitor is redirected to login. |
| M11.2 - Order detail, tracking, invoice | **Complete - scope note** | Same as M6.1-M11.1: no brief text was available this session, so scope was agreed as reasonable conventions. Real customer order detail page (`GET /Orders/{orderNumber}`), finally giving Milestone 11.1's list the per-row link it deliberately left out - reuses the existing, already-ownership-scoped `GetByOrderNumberAsync` (no Application-layer changes needed, since Milestone 10.3 already put shipment/tracking fields on `OrderDto`). **Tracking** is a simple status timeline (Placed -> Paid -> Shipped -> Delivered, with Cancelled/PaymentFailed/StockReservationFailed as their own terminal branches) plus the raw carrier/tracking-number/shipped-delivered-date card - no real carrier API exists, so this is presentation of data that was already there, the same "simulated, not integrated" posture the payment gateway established. **Invoice** is a separate print-optimized page (`GET /Orders/{orderNumber}/Invoice`, CSS-hides the site header/footer for printing) - only available for an order that was actually charged (redirects back to Details with a message otherwise, checked via `PaymentStatus == Succeeded` rather than order status, so a `Cancelled` order - which was genuinely charged and never refunded, Milestone 10.2 - still gets an invoice). **Fixed a real, self-inflicted bug along the way**: Confirmation's banner claimed "nothing has shipped yet" even when revisited long after a real shipment (Milestone 10.3) - removed the stale claim and added a "View order details" link into the new Details page instead of duplicating shipment rendering there. Manually verified against the real dev database: viewed a delivered order's detail page (correct timeline, tracking card, invoice link), printed its invoice, confirmed a declined order's invoice link is correctly absent and the direct URL redirects with an error, and confirmed one customer cannot view another's order or invoice by guessing the order number (404). |
| M11.3 - Reorder | **Complete - scope note** | Same as M6.1-M11.2: no brief text was available this session, so scope was agreed as reasonable conventions. This closes out Milestone 11 in its entirety (11.1 Dashboard & order list, 11.2 Order detail/tracking/invoice, 11.3 Reorder). A "Reorder these items" button on the customer order Details page (`POST /Orders/{orderNumber}/Reorder`) re-adds a past order's lines to the cart - **not gated by order status**, unlike Cancel/Ship/Deliver (Milestone 10.3's `OrderStatusTransitions`): this is a cart-population convenience, not an order-lifecycle operation, and is arguably most useful for a `PaymentFailed`/`StockReservationFailed` order where the customer wants to try again. New `ICartService.ReorderAsync` loops each line through the existing `AddItemAsync` (Milestone 6.1) independently, so one unavailable item (deactivated product, invalid variant, insufficient stock) is skipped with its real validation-error message rather than aborting the whole batch; the customer is redirected to `/Cart`, which already renders the resulting `TempData["Message"]`/`["Error"]` summary (the same banners Milestone 8.3's cart bug-fix wired up). Manually verified against the real dev database: placed an order, clicked Reorder from its Details page, confirmed the redirect to `/Cart` and the "Added 1 item to your cart" banner with the correct line item; the deactivated-product skip and cross-customer ownership block (404, same as Details/Invoice) are covered by integration tests. |
| M12.1 - Review submission & rating summary | **Complete - scope note** | Same as M6.1-M11.3: no brief text was available this session, so scope was agreed as reasonable conventions. Clean-slate build - no `Review`/`Rating` entity existed before, and the product page's "Reviews" tab was a static placeholder paragraph. New `Review` entity (`AuditableEntity`, one per `(UserId, ProductId)` via a unique index, mirroring `WishlistItem`'s own toggle constraint) and `IReviewService` (submission, rating summary, paged review list, `HasReviewedAsync`). **Any authenticated customer may review any product regardless of purchase history** - `IsVerifiedPurchase` (computed once at submission from order history, using the same "genuinely charged" `Paid`/`Shipped`/`Delivered`/`Cancelled` reasoning Milestone 11's `TotalSpent`/invoice eligibility already established) is a badge, not a gate. Reviewer identity is shown as "First name + last initial" (e.g. "Jane D.") - a privacy-conscious default with no prior precedent either way in this app. **No edit/delete, no moderation gate** - a review publishes immediately on submission; Milestone 12.2 ("Moderation & abuse protection") is explicitly the very next sub-milestone. The rating summary (average, count, a zero-filled 1-5 star breakdown) is computed live from the Reviews table on every read, matching this app's consistent "compute at read time" posture elsewhere (stock aggregation, tax/shipping estimates) rather than denormalizing onto `Product`. `ProductDetailService` now also depends on `IReviewService`, following the exact precedent `IWishlistService` set in Milestone 6.3 for enriching `ProductDetailDto` with a cross-domain flag (`IsWishlisted` -> now also `RatingSummary`/`Reviews`/`HasReviewed`). Manually verified against the real dev database: an anonymous visitor sees "Log in to write a review" instead of the form; a signed-in customer submits a review and immediately sees it along with the updated rating summary and star breakdown; the same customer, having genuinely purchased this product in an earlier milestone's manual verification, sees the "Verified Purchase" badge on their own review; and re-opening the tab shows "You've already reviewed this product" in place of the form. |
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
