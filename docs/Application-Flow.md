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
  Subtotal, Clear cart, and a disabled Checkout button (Milestone 8). A line
  for a product that's since become unpublished/inactive/deleted stays
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
