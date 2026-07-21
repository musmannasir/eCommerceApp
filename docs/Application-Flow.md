# Application Flow

## Status after Milestone 4.3

Cart/checkout/order flows don't exist yet (Milestone 6+); product detail
pages are Milestone 5. Milestone 4 (Storefront Home, Navigation and Product
Discovery) is now complete except for rating/best-selling filter and sort
options, which have no backing data until Milestones 12 and 9 - see
`Milestone-Status.md`'s "Deferred filter/sort options" note. What's live
today:

### Public / customer-facing (MVC, cookie auth)

- `GET /` - the real storefront home page: hero banner carousel, promo
  blocks, featured categories, featured/new-arrival/discounted products (all
  admin-managed or query-derived from real catalog data - see
  `Architecture.md`'s "Storefront home page composition" section), with
  honest placeholder sections for best sellers (needs Milestone 9 order
  data), recommendations, and recently-viewed (both Milestone 5).
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
  empty-result state. Product cards stay non-clickable (Milestone 5); brand
  names on cards and category cards are real links. See `Architecture.md`'s
  "Catalog listing pages" and "Search, filters, sorting, performance"
  sections for the query-shape and scope-decision reasoning.
- `GET /Search/Suggestions?q=` - JSON endpoint backing the header search
  box's debounced (300ms) suggestions dropdown: name, thumbnail, price,
  category, and a link to that product's own search-results page (product
  detail pages don't exist until Milestone 5).
- `GET /Account/Register`, `POST /Account/Register` - creates the account
  (assigned the `Customer` role), signs the user in immediately.
- `GET /Account/Login`, `POST /Account/Login` - validates credentials
  (generic error on failure, distinct message when locked out), signs in via
  cookie, redirects to `returnUrl` only if it passes `Url.IsLocalUrl`.
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
