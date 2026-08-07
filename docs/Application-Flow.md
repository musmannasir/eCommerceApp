# Application Flow

## Status: complete (M1 through M18.1)

Every route below is live in the finished application. This document was
written incrementally as each milestone landed, so most bullets still carry
their originating milestone number - that numbering is left in place as a
build history, not a forward-reference to something still pending. Two
storefront gaps mentioned below are permanent, deliberate scope boundaries
rather than "not yet built": rating/best-selling sort and filter options
(`CatalogSortOption`'s own code comment: "there is no rating or best-selling
signal" - reviews exist since Milestone 12 but were never wired into
sorting), the home page's "Best sellers" section (still a static
placeholder - `RecommendationService`'s own comment confirms "best selling"
was never added as a signal), and "Frequently Bought Together" on the
product detail page (still a static "Coming in a later milestone" note with
no later milestone left to add it). See `Milestone-Status.md` for the full
per-milestone scope record if you need the "why" behind any individual
route below.

### Public / customer-facing (MVC, cookie auth)

- `GET /` - the real storefront home page: hero banner carousel, promo
  blocks, featured categories, featured/new-arrival/discounted products (all
  admin-managed or query-derived from real catalog data - see
  `Architecture.md`'s "Storefront home page composition" section), with
  a real, cookie/DB-backed recently-viewed section (Milestone 5.3), and a
  permanent placeholder for "best sellers" - real order data has existed
  since Milestone 9, but nothing was ever wired up to rank by it (see the
  "Status" note above); "recommended for you" is likewise permanently
  absent from the home page itself, since there's no anchor product to
  score against there - see `Architecture.md`'s Milestone 5.3 section.
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
  records it for next time), a real ratings/reviews tab (Milestone 12.1 -
  see the Reviews section below), and a permanent "Frequently Bought
  Together" placeholder (never wired up - see the "Status" note above).
  404 for an unknown, unpublished, inactive, or soft-deleted product. See
  `Architecture.md`'s "Product detail page", "Variant resolution & pricing
  service", and "Recently viewed & recommendations" sections for the full
  reasoning.
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
  products excluded); this line stays an estimate against the store's
  default jurisdiction even now that `Address` exists (Milestone 8.1),
  because the cart is browsed before any address is chosen - real,
  destination-based tax appears once checkout starts (see the Checkout
  section below). As of Milestone 7.4, this is computed against each
  line's **post-discount**
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
  again instead of staying free. Same estimate-only reasoning as tax - the
  cart is browsed before checkout picks a real address and method - and
  hidden entirely rather than shown as $0.00/"Free" when no method is
  configured at all. See `Architecture.md`'s Milestone 7.3 and 7.4
  sections.
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
  button is enabled. Since Milestone 9.2, this step also renders a Payment
  section (card number, cardholder name, expiry month/year, CVV) with
  helper text naming the two Stripe test-card numbers that simulate a
  success/decline - this is not a real payment form.
- `POST /Checkout/PlaceOrder` (Milestone 8.3, now persisting a real `Order`
  as of Milestone 9.1, now charging a simulated payment as of Milestone
  9.2, now reserving stock first as of Milestone 9.3) - re-runs the full
  validation battery (cart availability, stock sufficiency, address
  ownership, shipping-method availability) one last time before submission,
  since any of it can change in the time between viewing Review and
  clicking the button; a stock or stale-shipping-method failure redirects
  back to `/Cart` or the Shipping step respectively, same as the equivalent
  `GET`-time guards. First checks for an existing order under the submitted
  idempotency token (`IOrderService.GetByIdempotencyKeyAsync`) - if found,
  skips straight to that order's Confirmation page without re-validating,
  re-reserving, or re-charging. Otherwise, on success, creates the
  `Order`/`OrderItem` rows, then reserves stock for each line (best-fit
  warehouse, `IInventoryService.ReserveStockAsync`), then - only if every
  line reserved successfully - charges the submitted card via the
  simulated `IPaymentGateway`, all inside the same call (`IOrderService
  .CreateOrderAsync`, keyed by the idempotency token - unique-indexed, so a
  genuine concurrent duplicate is caught by the database rather than a
  check-then-act gap, and can never be reserved or charged twice). If any
  line's stock can't be secured, no reservations are left dangling (the
  ones already made for this order are released) and the card is never
  charged - the order is still created, marked `StockReservationFailed`,
  with `StockIssueMessage` naming the affected line. Otherwise the card is
  charged once, synchronously - a decline still produces a real, persisted
  order (marked `PaymentFailed`, its reservations released too, not retried
  in place), and the cart is only cleared once the charge actually succeeds
  (Milestone 9.2 - a decline or a stock-reservation failure both leave the
  cart untouched so the customer can immediately retry). Redirects to
  `/Checkout/Confirmation/{orderNumber}` in all three outcomes.
- `GET /Checkout/Confirmation/{orderNumber}` (Milestone 8.3's cache-backed
  page, now backed by a real persisted `Order` as of Milestone 9.1) -
  `IOrderService.GetByOrderNumberAsync`, ownership-scoped exactly like
  `IAddressService.GetByIdAsync` (another customer's order number returns
  `NotFound`, never their data); shows the real outcome three ways
  (Milestone 9.3) - a success banner with the masked card/brand for `Paid`,
  a stock-issue banner naming the affected item and reason for
  `StockReservationFailed` (explicit that the payment method was never
  charged, and the Payment card is hidden entirely), or the existing decline
  banner with the gateway's decline reason for `PaymentFailed`. A missing
  or foreign order number redirects back to `/Checkout` with a "we couldn't
  find that order" message instead of erroring.
- `GET /Orders` (`[Authorize]`, Milestone 11.1) - "Your orders" dashboard:
  total-orders/total-spent cards, then a paged table (order number, placed
  date, item count, total, status) with a View link per row.
- `GET /Orders/{orderNumber}` (Milestone 11.2) - full order detail:
  status-step badges (Placed/Paid/Shipped/Delivered, or Payment
  failed/Stock issue/Cancelled), the shipping address and method, a
  Tracking card (carrier, tracking number, shipped/delivered timestamps -
  shown only once the order has shipped), a return-request status card
  once one exists, the payment card, and line items with totals. Same
  ownership scoping as Confirmation - another customer's order returns
  `NotFound`. Buttons/links shown conditionally by status: **Print
  invoice** (Paid/Shipped/Delivered only), **Reorder these items** (any
  status - re-adds the order's items to the cart and redirects to `/Cart`),
  **Cancel order** (Paid only - releases the stock reservation, no refund),
  **Request a return** (Delivered only, and only if no open return request
  already exists for this order).
- `GET /Orders/{orderNumber}/Invoice` (Milestone 11.2) - only reachable once
  `PaymentStatus` is `Succeeded`; otherwise redirects back to Details with
  an error.
- `POST /Orders/{orderNumber}/Reorder` (Milestone 11.3) - re-adds every
  line from the order to the current cart (not gated by order status -
  even a cancelled order's items can be reordered) and redirects to
  `/Cart/Index`.
- `POST /Orders/{orderNumber}/Cancel` (Milestone 10.2, self-service since
  11.2) - customer-initiated cancellation, only offered while `Status` is
  `Paid`; same release-reservation/no-refund behavior as the admin
  equivalent.
- `GET`/`POST /Orders/{orderNumber}/Return` (Milestone 13.1/13.2) - the
  return-request form (reason dropdown: Defective, Wrong item, No longer
  needed, Not as described, Other, plus a comment) and its submission,
  only offered once the order is `Delivered` and has no open
  (Requested/Approved) return request already. Submitting redirects back to
  Details, which now shows the request's status; staff decide it from
  `/Admin/Returns` (see the Admin section below).
- `POST /Product/{slug}/Review` (`[Authorize]`, rate-limited, Milestone
  12.1) - submits a rating (1-5) and body text for the product on whose
  page the form appears; only shown if the signed-in customer hasn't
  already reviewed this product (one review per (user, product), enforced
  by a unique index). `IsVerifiedPurchase` is set automatically from the
  customer's own order history (a genuinely charged order for this
  product), not something the customer can claim. The product detail
  page's rating summary (average, star display, count) updates
  immediately.
- `POST /Product/{slug}/Review/{reviewId}/Report` (`[Authorize]`,
  rate-limited, Milestone 12.2) - flags a review for moderation with a
  reason and optional comment; feeds the admin `/Admin/Reviews` queue. At
  most one report per (review, reporter).
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

- `GET /Admin/Home/Index` - the admin dashboard. Requires one of
  `SuperAdmin`/`Admin`/`CatalogManager`/`InventoryManager`/`OrderManager`/
  `CustomerSupport` (`Roles.StaffRolesCsv`); anonymous requests redirect to
  login, authenticated non-staff (e.g. `Customer`) get redirected to
  `/Home/AccessDenied` (403). Since Milestone 14.1, staff who additionally
  hold `CanViewFinancialReports` (`SuperAdmin`/`Admin`) see real KPI cards
  here (total revenue, total refunded, net revenue, average order value,
  paid orders, refunds issued) plus a link into the Ledger - see the
  Finance bullets further down.
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
  and Max total uses/Max uses per customer - still recorded but not
  enforced in the finished app; a note to that effect is shown right on the
  form. See `Admin-User-Guide.md`.
- `/Admin/TaxRates` - Milestone 7.2, tax rate CRUD (soft delete + recycle
  bin) under the "Checkout" nav section, gated by `CanManageCatalog` (no
  separate Checkout/Finance policy exists). Country code (ISO alpha-2),
  optional region code (blank = whole-country rate), tax category (matched
  against a product's Tax Category by plain string, case-insensitive), and
  a percentage rate. Feeds two different things: the storefront Cart page's
  early "Estimated tax" line (against the store's configured default
  jurisdiction, before an address is known), and - since Milestone 7.4/8 -
  the real, destination-based tax on the Checkout Review step and the
  finished Order, computed against the customer's actual chosen address.
  See `Admin-User-Guide.md`.
- `/Admin/ShippingMethods` - Milestone 7.3, shipping method CRUD (soft
  delete + recycle bin) in the same "Checkout" nav section as Tax Rates,
  gated by `CanManageCatalog`. Country code, optional region code (blank =
  whole-country method), a base rate plus a per-kg rate (using
  `Product.Weight`), an optional free-shipping subtotal threshold, and an
  optional estimated delivery-day range. Unlike Tax Rates, several named
  methods can coexist for the same jurisdiction (e.g. Standard and
  Express). Feeds both the Cart page's early "Estimated shipping" line
  (cheapest active method for the store's default jurisdiction) and, since
  Milestone 7.4/8, the real Checkout Shipping step's full list of options
  for the customer's actual chosen address, with real method selection.
  See `Admin-User-Guide.md`.
- `/Admin/Orders` - order queue (search by order number/customer name,
  status filter) and order detail page, gated by `CanManageOrders`
  (`SuperAdmin`/`Admin`/`OrderManager`/`CustomerSupport`). From the detail
  page: Cancel (only while Paid - releases the stock reservation, no
  refund), Mark shipped (only while Paid - requires a carrier and tracking
  number, creates the order's `Shipment`), Mark delivered (only while
  Shipped), and a staff-only Internal notes field never shown to the
  customer. See `Admin-User-Guide.md`.
- `/Admin/Reviews` - review moderation queue (Milestone 12.2): every review
  with at least one open report, gated by `CanManageOrders`. Dismiss clears
  the reports and leaves the review live; Remove soft-deletes it. See
  `Admin-User-Guide.md`.
- `/Admin/Returns` - return request queue (Milestone 13.2/13.3), gated by
  `CanManageOrders`, split into "Pending decision" (Approve/Reject) and
  "Awaiting receipt" sections. The refund action ("Mark received & refund")
  additionally requires `CanProcessRefunds`
  (`SuperAdmin`/`Admin`/`OrderManager` - deliberately narrower, excludes
  `CustomerSupport`) and, in one call, issues the refund and restocks the
  returned inventory. See `Admin-User-Guide.md`.
- `/Admin/Ledger` - Milestone 14.1/14.2, gated by `CanViewFinancialReports`
  (`SuperAdmin`/`Admin` only). The merged charge/refund transaction ledger
  with CSV export, plus a Cash Flow view (day-by-day revenue/refunded/net,
  date-range filterable, its own CSV export). The dashboard's KPI cards
  (`/Admin/Home/Index` - total revenue, total refunded, net revenue,
  average order value, paid orders, refunds issued) are gated by the same
  policy and simply don't render for a staff member who doesn't hold it.
  See `Admin-User-Guide.md`.
- `/Admin/Reports` - Milestone 14.3, gated by `CanViewFinancialReports`. A
  hub linking to Ledger, Cash Flow, and Top Selling Products (date-range
  filterable, its own CSV export). See `Admin-User-Guide.md`.
- `/Admin/Users` - Milestone 16.1, gated by `CanManageUsers`
  (`SuperAdmin`/`Admin` only) - distinct from `AccountController`'s
  self-service register/login/change-own-password. Create an account with a
  role, edit name/role, activate/deactivate, unlock a locked-out account,
  and send a password-reset email on a user's behalf (reuses the same
  `IAuthService.ForgotPasswordAsync` flow the customer-facing forgot-password
  page uses). Editing your own account disables the role/deactivation
  controls, so an admin can't lock themselves out. See `Admin-User-Guide.md`.
- `/Admin/AuditLog` - Milestone 16.2, gated by `CanManageUsers`. Reads the
  same `SecurityAuditEvents` table Milestone 1 created, filterable by date
  range, event type, outcome, and user email, with CSV export. See
  `Admin-User-Guide.md`.
- `/Admin/Settings` - Milestone 16.3, gated by `CanManageUsers`. A
  single-row editor (`StoreSettings`, not a list) for store name, currency,
  default country, whether displayed prices include tax, the recently-viewed
  section's max item count, and the default tax/shipping jurisdiction codes
  the Cart page's early estimates use before a real address exists.
  Optimistic concurrency via `RowVersion`, since two admins could edit this
  shared row at once. See `Admin-User-Guide.md`.

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

- `GET /health/live`, `GET /health/ready` - `/health/ready` now (Milestone
  17.3) has a bounded 5-second timeout on its SQL Server check, so a hung
  database makes it fail fast rather than hang indefinitely.
- Unmapped routes still resolve to the branded 404 page.
- Every response gets security headers (`Content-Security-Policy`,
  `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`) and, where
  applicable, CORS handling (Milestone 17.1) - see `Security.md`.
- Responses are Brotli/gzip-compressed (Milestone 17.2) where the client
  accepts it.
- Two background/async pieces have no HTTP route of their own:
  `OutboxProcessingBackgroundService` (Milestone 15.3) drains pending
  `OutboxMessages` (order-confirmation and password-reset emails) on a
  timer, and ASP.NET Core Data Protection keys persist to disk
  (`DataProtection-Keys/` by default, Milestone 17.2) so cookies/tokens
  survive an app restart.

This document is complete as of Milestone 18.1 - every route the finished
application serves, across storefront, admin, and API, is listed above.
