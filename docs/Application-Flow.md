# Application Flow

## Status after Milestone 6.3

Checkout/order flows don't exist yet (Milestone 7+). Milestone 4
(Storefront Home, Navigation and Product Discovery) is complete except for
rating/best-selling filter and sort options, which have no backing data
until Milestones 12 and 9 - see `Milestone-Status.md`'s "Deferred filter/sort
options" note. Milestone 5.1 added the product detail page; Milestone 5.2
made variant switching live (no page reload) and added the central
`IPricingService`; Milestone 5.3 closed out Milestone 5 with real
recently-viewed tracking and category/brand/tag/price-based recommendations.
Milestone 6.1 added cart core - the Add to Cart button (disabled since
Milestone 5.1) is now live, and a `/Cart` page manages what's in it.
Milestone 6.2 added cart merge on sign-in and pricing/stock integrity
notices. Milestone 6.3 closes out Milestone 6 with an account-only
wishlist. What's live today:

### Public / customer-facing (MVC, cookie auth)

- `GET /` - the real storefront home page: hero banner carousel, promo
  blocks, featured categories, featured/new-arrival/discounted products (all
  admin-managed or query-derived from real catalog data - see
  `Architecture.md`'s "Storefront home page composition" section), with
  a real, cookie/DB-backed recently-viewed section (Milestone 5.3), and
  honest placeholder sections for best sellers (needs Milestone 9 order
  data) and "recommended for you" (no anchor product to score against on
  the home page - see `Architecture.md`'s Milestone 5.3 section).
- The public layout (every page) renders a real, live, now-clickable
  category nav via `CategoryNavViewComponent`.
- `GET /Products` - all active/published products, paginated.
- `GET /Category/{slug}` - products in that category and its active
  subcategories; 404 if the slug doesn't match an active category.
- `GET /Brand/{slug}` - products for that brand; 404 if the slug doesn't
  match an active brand.
- `GET /Brands` - index of all active brands.
- `GET /Search?q=` - substring match against product name/SKU/brand/category/
  tags/short description/keywords, with relevance-ranked results (name
  starts-with > contains > tie-break by name). All four listing routes
  (`/Products`, `/Category/{slug}`, `/Brand/{slug}`, `/Search`) share one
  view and query API: combinable filters (price range, category,
  subcategory, brand, stock availability, discounted, featured, attributes,
  new arrivals) and sorting (relevance, newest, price asc/desc, largest
  discount, name A-Z/Z-A), all persisted in the query string; grid/list
  toggle; pagination; an active-filter summary with clear-filter links; an
  empty-result state. Product cards, brand names, and category cards are all
  real links now. See `Architecture.md`'s "Catalog listing pages" and
  "Search, filters, sorting, performance" sections for the query-shape and
  scope-decision reasoning.
- `GET /Product/{slug}` - the product detail page: gallery with click-to-zoom,
  price/compare-at/discount%/tax indicator (via the central `IPricingService`),
  aggregated stock status, per-attribute variant selectors that resolve live
  (no page reload) - the client disables dropdown options that can't form a
  real variant given the other selections, and a strict server-side
  `Resolve` call confirms and displays the authoritative SKU/price/stock/image
  for any exact match - quantity selector with a live Add to Cart button
  (Milestone 6.1) and a wishlist toggle button (Milestone 6.3, account-only -
  an anonymous click redirects to login), description, specifications, warranty/returns/shipping, real
  related products (category/brand/tag/price-scored recommendations,
  Milestone 5.3), a real recently-viewed section (viewing this page also
  records it for next time), and honest placeholders for ratings/reviews/
  frequently-bought-together. 404 for an unknown, unpublished, inactive,
  or soft-deleted product. See `Architecture.md`'s "Product detail page",
  "Variant resolution & pricing service", and "Recently viewed &
  recommendations" sections for the full reasoning.
- `GET /Product/{slug}/Resolve?variantId=` - JSON endpoint backing the live
  variant switcher; rejects a variant that doesn't exist, isn't active, or
  doesn't belong to the product (Milestone 5.2).
- `GET /Search/Suggestions?q=` - JSON endpoint backing the header search
  box's debounced (300ms) suggestions dropdown: name, thumbnail, price,
  category, and a link to that product's own search-results page (product
  detail pages don't exist until Milestone 5).
- `GET /Cart` - the cart page: every line item (image, name, variant, SKU,
  unit price, quantity, line total), a stock/unavailable badge per line, a
  Subtotal, Clear cart, and (since Milestone 8.2) a real Checkout button
  linking into the Checkout flow below - previously disabled from Milestone
  6.1 through 8.1. A line for a product that's since become
  unpublished/inactive/deleted stays
  visible (marked "No longer available") but is excluded from the Subtotal -
  only Remove works on it. Since Milestone 6.2, a line also shows a "price
  changed since you added this" note when the live price no longer matches
  what it was when added, and a "you have more than are available" note when
  stock has shrunk below the line's quantity - neither one silently changes
  anything, both just inform.
- `GET /Cart/Summary` - JSON endpoint backing the header's Cart badge
  (`{ itemCount }`); renders on every page via `CartSummaryViewComponent`.
- `POST /Cart/Add`, `POST /Cart/UpdateQuantity`, `POST /Cart/Remove`,
  `POST /Cart/Clear` - AJAX JSON endpoints, CSRF-protected via the
  `X-CSRF-TOKEN` request header (see `_Layout.cshtml`'s `csrf-token` meta
  tag) since there's no posted `<form>`. Add rejects a missing/invalid
  variant selection and a quantity beyond available stock (untracked
  inventory and backorder-allowed items are exempt from the stock cap, the
  same leniency the product detail page already applies); UpdateQuantity
  additionally rejects an item that's become unavailable since it was added.
- `POST /Cart/ApplyCoupon`, `POST /Cart/RemoveCoupon` - Milestone 7.1, same
  AJAX/CSRF-header pattern as every other Cart endpoint. Applying validates
  the code (active, in its date window, minimum order met, and - for a
  Category/Brand/Product-scoped promotion - at least one matching line in
  the cart) and replaces whatever was applied before (at most one at a
  time, no stacking); an invalid code returns a `400` with a message shown
  inline on the Cart page, the cart itself untouched. The Cart page's
  summary now shows the applied coupon code/name with a Remove button (or
  an input + Apply button if none is applied), Subtotal, Discount (when
  any), and Total - `Subtotal` keeps its pre-discount meaning, `Total` is
  `Subtotal` minus the discount. A previously-applied coupon that's since
  become invalid (expired, deactivated, or its scoped item no longer in the
  cart) is silently cleared the next time the cart is read - same
  "re-derive truth on every read" pattern as an unavailable cart line - see
  `Architecture.md`'s Milestone 7.1 section.
- The Cart page's summary also shows an **Estimated tax** line (Milestone
  7.2) when at least one tax rate is configured for the store's default
  jurisdiction - computed per line by `Product.TaxCategory` (non-taxable
  products excluded), since there's no real customer destination to
  calculate against yet (`Address` arrives in Milestone 8.1). As of
  Milestone 7.4, this is computed against each line's **post-discount**
  amount (via the Checkout Calculation Service, which allocates the cart's
  Promotion discount across the lines it actually applies to), not the raw
  line total. The line is hidden entirely - not shown as $0.00 - when no
  rate is configured at all, so the store doesn't appear to be untaxed by
  default. See `Architecture.md`'s Milestone 7.2 and 7.4 sections.
- The Cart page's summary also shows an **Estimated shipping** line
  (Milestone 7.3), the cheapest active shipping method for the store's
  default jurisdiction - cost is a base rate plus a per-kg rate applied to
  the cart's total weight (from `Product.Weight`, treating a missing weight
  as 0kg), waived entirely once a method's free-shipping threshold is met.
  As of Milestone 7.4, the threshold is checked against the **post-discount**
  subtotal (via the Checkout Calculation Service), so a coupon that drops
  the subtotal below the threshold correctly starts charging shipping
  again instead of staying free. Same estimate-only reasoning as tax - no
  real destination until Milestone 8.1, no method-picker UI until Milestone
  8.2 - and hidden entirely rather than shown as $0.00/"Free" when no
  method is configured at all. See `Architecture.md`'s Milestone 7.3 and
  7.4 sections.
- The Cart page's summary also shows an **Estimated total** line (Milestone
  7.4), `Total + EstimatedTax + EstimatedShipping` - shown whenever either
  tax or shipping is configured, still just an estimate for the same
  reasons as its components. See `Architecture.md`'s Milestone 7.4 section.
- `GET /Wishlist` (`[Authorize]`) - Milestone 6.3, account-only (no guest
  wishlist, unlike Cart). Every saved product, most-recently-added first,
  with a Remove button; a product that's since become unpublished/inactive/
  deleted is silently excluded from the list, not flagged like Cart's - a
  wishlist is browsing-adjacent, not a committed purchase intent.
- `POST /Wishlist/Toggle` (`[Authorize]`) - AJAX JSON endpoint backing the
  heart/toggle button on the product detail page (not on every product card
  sitewide - see `Architecture.md`'s Milestone 6.3 section); adds the
  product if not already saved, removes it if it is, returns
  `{ isWishlisted, itemCount }`. An anonymous request gets a real `401`
  (Milestone 6.3 also fixed the default cookie-auth challenge, which
  otherwise `fetch()` would silently follow as a `200`-with-login-HTML
  response) so the client-side JS can redirect to `/Account/Login`.
- `POST /Wishlist/Remove` (`[Authorize]`) - explicit removal for the
  wishlist page's Remove button; idempotent, no error if the item's already
  gone.
- `GET /Addresses` (`[Authorize]`) - Milestone 8.1, account-only address
  book, linked from the Profile page ("Manage addresses"). Every saved
  address, most-recently-updated first, each with Edit/Delete and a "Set as
  default" button (hidden on whichever address is already the default).
- `GET`/`POST /Addresses/Create`, `GET`/`POST /Addresses/Edit/{id}` -
  classic server-rendered forms (not AJAX, unlike Cart/Wishlist), same
  pattern as `AccountController`'s `ChangePassword`. A customer's first-ever
  address is always saved as the default regardless of the form's checkbox;
  marking any other address as the default clears the flag from whichever
  address had it before. An `{id}` belonging to a different customer's
  address returns a plain `404`, not their address's data. `Create` accepts
  an optional `?returnUrl=` (Milestone 8.2 - Checkout redirects here when a
  customer has no saved addresses yet, and lands back in Checkout once
  they've saved one) with the same local-only open-redirect check as
  `AccountController`'s `ReturnUrl`.
- `POST /Addresses/Delete/{id}` - removes the address; deleting the current
  default leaves no default at all rather than silently promoting another
  address, matching the "don't guess on the customer's behalf" reasoning
  Cart already applies to an invalidated promotion.
- `POST /Addresses/SetDefault/{id}` - marks this address as the default,
  clearing the flag from whichever address had it before.
- `GET`/`POST /Checkout` (`[Authorize]`) - Milestone 8.2, step 1 of the
  checkout flow: pick a shipping address from the customer's saved address
  book (default pre-selected). Redirects to `/Cart` if the cart is empty, or
  to `/Addresses/Create?returnUrl=/Checkout` if the customer has no saved
  addresses yet. Since Milestone 8.3, this `GET` also redirects to `/Cart`
  with an error if any cart item now exceeds available stock (re-checking
  the same `CartItemDto.QuantityExceedsStock` signal the Cart page has
  shown informationally since Milestone 6.2).
- `GET`/`POST /Checkout/Shipping?addressId=` - step 2: pick a shipping
  method from every option available for the chosen address's jurisdiction
  (cheapest pre-selected), computed against the cart's post-discount
  subtotal - the same Milestone 7.4 fix, now applied against a real
  address instead of the store's default jurisdiction. An address that
  doesn't belong to the current customer, or a jurisdiction with no
  shipping methods configured at all, both redirect/resolve sensibly rather
  than erroring.
- `GET /Checkout/Review?addressId=&shippingMethodId=` - step 3: the real,
  destination-based order total via `ICheckoutCalculationService
  .CalculateAsync` (Milestone 7.4) - subtotal, discount, tax, shipping, and
  grand total, all computed against the chosen address and shipping
  method, not an estimate. A stale or tampered `shippingMethodId` (doesn't
  match any option for this address) redirects back to the Shipping step.
  Since Milestone 8.3, this step also renders a fresh, single-use
  idempotency token (a hidden field on the "Place order" form) and the
  button is enabled.
- `POST /Checkout/PlaceOrder` (Milestone 8.3) - re-runs the full validation
  battery (cart availability, stock sufficiency, address ownership,
  shipping-method availability) one last time before submission, since any
  of it can change in the time between viewing Review and clicking the
  button; a stock or stale-shipping-method failure redirects back to
  `/Cart` or the Shipping step respectively, same as the equivalent
  `GET`-time guards. On success, caches the validated result (keyed by the
  submitted idempotency token, 15-minute `IMemoryCache` TTL - see
  `Architecture.md`) and redirects to `/Checkout/Confirmation?key=`.
  Resubmitting the same token (double-click, back button, network retry)
  replays the original cached outcome instead of re-validating - so a
  submission that already succeeded can't be turned into a failure by
  something changing afterward. `Order` entities still don't exist until
  Milestone 9.1, so nothing is actually placed - see `Confirmation` below.
- `GET /Checkout/Confirmation?key=` (Milestone 8.3) - reads the cached
  result for the given idempotency token; explicit that "your order
  details have been validated" is not a real placed order ("nothing has
  been charged or shipped yet"). A missing/expired/foreign token redirects
  back to `/Checkout` with a "checkout session has expired" message.
- `GET /Account/Register`, `POST /Account/Register` - creates the account
  (assigned the `Customer` role), signs the user in immediately, then folds
  any guest cart into the new account (Milestone 6.2 - see
  `Architecture.md`'s "Cart merge & pricing integrity" section).
- `GET /Account/Login`, `POST /Account/Login` - validates credentials
  (generic error on failure, distinct message when locked out), signs in via
  cookie, merges any guest cart into the account's cart the same way
  Register does, redirects to `returnUrl` only if it passes `Url.IsLocalUrl`.
- `POST /Account/Logout` - clears the cookie.
- `GET/POST /Account/ForgotPassword` - always shows the same confirmation
  regardless of whether the email is registered; sends a reset email (dev:
  written to `Logs/DevEmails/*.html`) only when it is.
- `GET/POST /Account/ResetPassword` - consumes the token, updates the
  password, revokes all the user's active refresh tokens.
- `GET/POST /Account/ChangePassword` (`[Authorize]`) - requires the current
  password, refreshes the cookie (since Identity rotates the security stamp
  on password change), revokes all active refresh tokens.
- `GET /Account/Profile` (`[Authorize]`) - name, email, roles, member-since,
  last-login.
- `POST /Account/RevokeAllSessions` (`[Authorize]`) - revokes every refresh
  token and bumps the security stamp, then signs the current session out too.

### Admin (MVC, cookie auth, role-gated)

- `GET /Admin/Home/Index` - dashboard placeholder. Requires one of
  `SuperAdmin`/`Admin`/`CatalogManager`/`InventoryManager`/`OrderManager`/
  `CustomerSupport` (`Roles.StaffRolesCsv`); anonymous requests redirect to
  login, authenticated non-staff (e.g. `Customer`) get redirected to
  `/Home/AccessDenied` (403).
- `/Admin/Categories`, `/Admin/Brands`, `/Admin/ProductAttributes`,
  `/Admin/Products` - full catalog CRUD, gated by the `CanManageCatalog`
  policy (`SuperAdmin`/`Admin`/`CatalogManager`). See `Admin-User-Guide.md`
  for the full feature list.
- `/Admin/Warehouses`, `/Admin/Inventory` - warehouse CRUD, opening-stock
  recording, manual adjustments, movement history, low/out-of-stock views,
  gated by the `CanManageInventory` policy
  (`SuperAdmin`/`Admin`/`InventoryManager`). See `Admin-User-Guide.md`.
- `/Admin/Suppliers` - supplier CRUD (soft delete + recycle bin) and
  supplier-to-product linking (supplier SKU, cost, lead time, preferred
  flag), same `CanManageInventory` policy. See `Admin-User-Guide.md`.
- `/Admin/PurchaseOrders` - purchase-order lifecycle (Draft → Submitted →
  Approved → PartiallyReceived/Received, or Cancelled), item management,
  full/partial goods receipt with audited over-receipt override, same
  `CanManageInventory` policy. Receiving updates the matching `InventoryItem`
  and writes a linked `StockMovement`. See `Admin-User-Guide.md`.
- `/Admin/HomePageBanners` - hero/promo banner CRUD (soft delete + recycle
  bin, image upload), gated by `CanManageCatalog`. Feeds the public home
  page. See `Admin-User-Guide.md`.
- `/Admin/Promotions` - Milestone 7.1, promotion/coupon CRUD (soft delete +
  recycle bin), gated by `CanManageCatalog` (same policy as Home Page
  Banners - no separate Marketing policy exists). Discount type
  (Percentage/Fixed amount), scope (Entire order/Category/Brand/Product,
  with a matching dropdown that only shows the field the selected scope
  needs), minimum order amount, max discount cap, a start/end date window,
  and Max total uses/Max uses per customer (recorded but not yet enforced -
  a note to that effect is shown right on the form). See
  `Admin-User-Guide.md`.
- `/Admin/TaxRates` - Milestone 7.2, tax rate CRUD (soft delete + recycle
  bin) under a new "Checkout" nav section, gated by `CanManageCatalog` (no
  separate Checkout/Finance policy exists yet). Country code (ISO alpha-2),
  optional region code (blank = whole-country rate), tax category (matched
  against a product's Tax Category by plain string, case-insensitive), and
  a percentage rate. Feeds the storefront Cart page's "Estimated tax" line
  against the store's configured default jurisdiction - not real,
  destination-based tax, which arrives with Checkout (Milestones 7.4/8).
  See `Admin-User-Guide.md`.
- `/Admin/ShippingMethods` - Milestone 7.3, shipping method CRUD (soft
  delete + recycle bin) in the same "Checkout" nav section as Tax Rates,
  gated by `CanManageCatalog`. Country code, optional region code (blank =
  whole-country method), a base rate plus a per-kg rate (using
  `Product.Weight`), an optional free-shipping subtotal threshold, and an
  optional estimated delivery-day range. Unlike Tax Rates, several named
  methods can coexist for the same jurisdiction (e.g. Standard and
  Express). The cheapest active method for the store's configured default
  jurisdiction feeds the storefront Cart page's "Estimated shipping" line -
  not real, destination-based shipping or method selection, which arrive
  with Checkout (Milestones 7.4/8). See `Admin-User-Guide.md`.

### API (`/api/v1/auth`, JWT bearer)

- `POST register` - creates the account and returns `{ user, tokens }`.
- `POST login` - validates credentials and returns `{ user, tokens }`.
- `POST refresh` - rotates the refresh token; reuse of an already-rotated
  token revokes the whole chain for that user.
- `POST logout` (`[Authorize]`) - revokes the presented refresh token.
- `POST revoke-all` (`[Authorize]`) - revokes every refresh token for the
  caller.
- `GET me` (`[Authorize]`) - the caller's profile.

### Infra

- `GET /health/live`, `GET /health/ready` - unchanged from the Foundation
  milestone.
- Unmapped routes still resolve to the branded 404 page.

This document is filled in feature-by-feature as each remaining milestone
lands (browsing, cart, checkout, order processing, returns, etc.).
