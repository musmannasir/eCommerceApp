# Admin User Guide

## What you can do (complete, as of Milestone 18.1)

- **Log in as the seeded SuperAdmin**: configure `SeedAdmin:Email` /
  `SeedAdmin:Password` via User Secrets (see README.md), start the app once
  so it seeds the account, then log in at `/Account/Login` with those
  credentials.
- **Access the Admin dashboard**: `/Admin/Home/Index`, reachable by any
  staff role (`SuperAdmin`, `Admin`, `CatalogManager`, `InventoryManager`,
  `OrderManager`, `CustomerSupport`). Customers are redirected to an
  "Access denied" page if they try.

### Catalog (`SuperAdmin`/`Admin`/`CatalogManager`)

- **Categories** (`/Admin/Categories`): searchable/sortable/paginated list,
  tree view, create/edit, deactivate/activate, delete (soft, with a recycle
  bin to restore), unlimited nesting with circular-reference protection.
- **Brands** (`/Admin/Brands`): same CRUD pattern as Categories, plus a logo
  upload.
- **Product Attributes** (`/Admin/ProductAttributes`): define reusable
  attributes (Color, Size, Storage, ...) and their values - used when adding
  product variants.
- **Products** (`/Admin/Products`): searchable/sortable/paginated list with
  category/brand filters, create/edit (prices, dimensions, warranty, return
  eligibility, SEO fields), publish/unpublish (publishing requires the
  product be active), deactivate/activate, delete/restore. The edit page has
  tabs for:
  - **Images**: upload JPEG/PNG/WebP (validated by real file content, not
    just the extension), optionally tied to a specific variant, with a
    primary-image flag.
  - **Variants**: pick one value per attribute (e.g. Color=Red) and a SKU to
    add a variant; duplicate SKUs and duplicate attribute combinations are
    rejected.
  - **Specifications**: free-form name/value rows (e.g. "Battery Life" ->
    "30 hours").
  - **Tags**: add/remove free-text tags.

### Inventory (`SuperAdmin`/`Admin`/`InventoryManager`)

- **Warehouses** (`/Admin/Warehouses`): create/edit warehouses, mark one as
  the default, deactivate/activate. The schema supports many warehouses even
  though a single one is typical for now.
- **Inventory overview** (`/Admin/Inventory`): every tracked product/variant
  across all warehouses, with on-hand/reserved/available quantities, reorder
  level, and status (InStock/LowStock/OutOfStock/Backorder). Filterable by
  warehouse and searchable by product name/SKU.
  - **Low stock** / **Out of stock** views: the same list pre-filtered.
  - **Record opening stock**: pick a warehouse and a product (and a specific
    variant, if the product has any), enter the first quantity count plus a
    reorder level/quantity and whether backorders are allowed. A
    product/variant can only have its opening stock recorded once per
    warehouse - after that, use "Adjust" to change the quantity.
  - **Adjust**: apply a signed quantity change (positive to add, negative to
    remove) with a required reason - e.g. a cycle-count correction or
    reporting damaged stock. Rejected if it would take on-hand below zero.
  - **History**: the full, permanent movement ledger for one inventory item -
    every opening stock, adjustment, reservation, and release, oldest state
    never overwritten.
- **Suppliers** (`/Admin/Suppliers`): searchable/sortable/paginated list,
  create/edit (contact details, address, notes), deactivate/activate, delete
  (soft, with a recycle bin to restore). The edit page also manages which
  products this supplier can source: link a product with an optional
  supplier SKU, cost price, lead time in days, and a "preferred supplier"
  flag; unlink removes the link outright (not soft-deleted, since a link is
  either present or not).
- **Purchase orders** (`/Admin/PurchaseOrders`): searchable/status-filterable/
  paginated list, create a Draft (supplier, warehouse, expected delivery
  date, notes), add/remove items while still Draft (picked from the
  supplier's linked products, with unit cost pre-filled from the link),
  Submit, Approve, Cancel (only while Draft/Submitted/Approved - not once
  any goods have been received), and Receive goods:
  - **Receive**: shows every line with an outstanding (ordered minus already
    received) quantity, lets you enter how much to receive now per line,
    supports partial receipt (order status becomes PartiallyReceived) and
    full receipt (status becomes Received). Receiving more than a line's
    outstanding quantity is rejected unless you check "Allow over-receipt"
    and provide a reason - both the override flag and reason are recorded
    on the goods receipt for audit. Every receipt increases the matching
    warehouse's on-hand stock and appears in that item's movement history
    (`PurchaseReceipt` type, linked back to the purchase order).
  - **Goods receipt history**: every past receipt against the order, with
    who received it, when, how much per line, and any override reason.

### Marketing (`SuperAdmin`/`Admin`/`CatalogManager`)

- **Home page banners** (`/Admin/HomePageBanners`): searchable/paginated
  list, create/edit (title, subtitle, link URL, Hero or Promo type, display
  order), deactivate/activate, delete (soft, with a recycle bin to restore).
  A banner is created without an image first, then an image is uploaded
  separately on the edit page (same flow as a brand's logo) - it does not
  appear on the storefront home page until an image is set. Hero banners
  appear as the home page's top carousel; Promo banners appear as a smaller
  grid further down the page.

- **Promotions** (`/Admin/Promotions`): searchable/paginated list,
  create/edit, deactivate/activate, delete (soft, with a recycle bin to
  restore). Each promotion has:
  - A **coupon code** (optional) - leave it blank for an automatic
    promotion; note that automatic promotions aren't applied to any cart
    yet (see the scope note below), so in practice every promotion you
    create today should have a code.
  - A **discount type** (Percentage or Fixed amount) and value.
  - A **scope** - Entire order, or a specific Category/Brand/Product (the
    form only shows the picker for whichever scope you choose). A
    category/brand/product-scoped promotion only discounts the matching
    items in the cart, not the whole order.
  - Optional **minimum order amount** (checked against the cart's full
    subtotal) and **maximum discount amount** (caps a percentage
    discount's currency value).
  - A **start date** (required) and optional **end date**.
  - Optional **max total uses** / **max uses per customer** - these are
    recorded but not enforced anywhere; the form says so.
  - **Active/Inactive** - an inactive promotion's code stops working
    immediately, even mid-window.

  On the storefront, a customer applies a coupon code on the Cart page;
  only one promotion can be applied to a cart at a time (applying a new
  one replaces the last). If a promotion becomes invalid after being
  applied - you deactivate it, its window ends, or the customer removes
  the item it was scoped to - it's silently dropped from the cart the next
  time the customer looks at it, no error shown.

### Checkout (`SuperAdmin`/`Admin`/`CatalogManager`)

- **Tax rates** (`/Admin/TaxRates`): searchable/paginated list, create/edit,
  deactivate/activate, delete (soft, with a recycle bin to restore). Each
  rate has:
  - A **country code** (2-letter ISO, e.g. `US`, `PK`).
  - An optional **region code** - leave it blank for a whole-country rate;
    fill it in (e.g. a US state) for a rate that only applies there. If
    both a whole-country rate and a region-specific rate exist for the
    same country and category, the region-specific one wins for that
    region.
  - A **tax category** - must match a product's Tax Category field exactly
    (case-insensitive) to apply; there's no dropdown tying the two
    together, so a typo in either place means the rate silently won't
    match anything.
  - A **rate percentage** (0-100).
  - **Active/Inactive** - an inactive rate stops applying immediately.

  These rates power two things: the **Estimated tax** line on the
  customer-facing Cart page (before checkout, calculated against the
  store's configured default jurisdiction - set on the **Settings** page,
  see below - since there's no real shipping address known yet), and the
  real, destination-based tax on the Checkout Review step and the finished
  Order, calculated against the customer's actual chosen address. Either
  way, the line/amount only appears once at least one matching rate is
  configured; until then, nothing is shown rather than a misleading $0.00.

- **Shipping methods** (`/Admin/ShippingMethods`): searchable/paginated
  list, create/edit, deactivate/activate, delete (soft, with a recycle bin
  to restore). Unlike tax rates, more than one named method can exist for
  the same country/region (e.g. both "Standard" and "Express"). Each
  method has:
  - A **country code** and optional **region code**, same rules as tax
    rates.
  - A **base rate** and a **rate per kg** - the cost is the base rate plus
    the rate per kg multiplied by the order's total weight (a product with
    no weight recorded contributes 0kg).
  - An optional **free shipping threshold** - the method's cost becomes 0
    once the order subtotal meets this amount.
  - An optional **estimated delivery day range**.
  - **Active/Inactive**.

  The *cheapest* active method for the store's configured default
  jurisdiction powers the Cart page's early **Estimated shipping** line,
  same reasoning as tax; the line only appears once at least one method is
  configured. At checkout, the customer instead sees and picks from every
  active method available for their actual chosen address, with the real
  cost and (if applicable) free-shipping threshold already reflecting any
  applied coupon.

### Orders (`SuperAdmin`/`Admin`/`OrderManager`/`CustomerSupport`)

- **Order queue** (`/Admin/Orders`): search by order number or customer
  name, filter by status (Pending, Paid, PaymentFailed,
  StockReservationFailed, Cancelled, Shipped, Delivered), **Open** a row to
  see the full detail page.
- **Order detail**: shipping address, shipping method, payment (masked
  card/brand or decline reason), shipment tracking once shipped, every
  line item, and totals. Actions available depend on the order's current
  status:
  - **Cancel order** (Paid only): releases the order's reserved stock and
    marks it Cancelled. Does **not** process a refund - a cancelled order
    was never delivered, so there's nothing to return; a refund only
    follows an approved, received return (see Returns below).
  - **Mark shipped** (Paid only): enter a carrier and tracking number
    (both required) to record the shipment and move the order to Shipped.
    This consumes the stock reservation for good; a shipped order can no
    longer be cancelled.
  - **Mark delivered** (Shipped only): moves the order to Delivered, which
    is what makes it eligible for a customer-initiated return request.
  - **Internal notes**: a staff-only free-text field, saved independently,
    never shown to the customer - useful for handoffs between staff
    members on a tricky order.

### Reviews (`SuperAdmin`/`Admin`/`OrderManager`/`CustomerSupport`)

- **Review moderation queue** (`/Admin/Reviews`): shows every review with
  at least one open customer report (a badge shows how many). Reviews with
  no reports don't appear here at all - there's no general review-browsing
  list, only a moderation queue.
  - **Dismiss reports**: clears all reports on the review; it stays live
    and can be reported again later if warranted.
  - **Remove review**: soft-deletes the review entirely.

### Returns & Refunds (`SuperAdmin`/`Admin`/`OrderManager`/`CustomerSupport`; refunding also needs `SuperAdmin`/`Admin`/`OrderManager`)

- **Return requests** (`/Admin/Returns`), split into two sections:
  - **Pending decision**: a customer's return request, with their chosen
    reason and comment. **Approve** authorizes the return (staff now
    expect the item shipped back) or **Reject** with a required reason -
    neither one refunds or restocks anything yet.
  - **Awaiting receipt**: approved returns waiting for the item to
    physically arrive back. **Mark received & refund** confirms receipt
    and, in one step, issues the refund and restocks the returned
    inventory. This action needs a narrower permission than viewing the
    queue (`CustomerSupport` can see and approve/reject, but can't issue
    money) - if you don't see the button, that's why.

### Finance (`SuperAdmin`/`Admin` only)

- **Dashboard** (`/Admin/Home/Index`): KPI cards - total revenue, total
  refunded, net revenue, average order value, paid orders, refunds issued -
  plus a link into the full ledger. Staff without this permission simply
  don't see these cards; everyone else's dashboard is unaffected.
- **Ledger** (`/Admin/Ledger`): every charge and refund merged into one
  transaction table (date, type, order, amount), with **Export CSV**.
- **Cash Flow** (`/Admin/Ledger/CashFlow`): day-by-day revenue/refunded/net
  bars over a date range (defaults to the last 30 days), with its own
  **Export CSV**.

### Reports (`SuperAdmin`/`Admin` only)

- **Reports hub** (`/Admin/Reports`): links to Ledger, Cash Flow, and Top
  Selling Products.
- **Top Selling Products** (`/Admin/Reports/TopSellingProducts`):
  date-range filterable table of quantity sold and revenue per product,
  with **Export CSV**.

### Users (`SuperAdmin`/`Admin` only)

- **User management** (`/Admin/Users`) - distinct from a customer's own
  self-service profile page; this is where staff manage *every* account,
  customer or staff. Search and filter by role/active status.
  - **New user**: create an account directly with a chosen role.
  - **Edit**: change a user's name or role.
  - **Activate**/**Deactivate**: a permanent admin-controlled disable,
    separate from a temporary lockout (too many failed logins).
  - **Unlock account**: clears a temporary lockout early.
  - **Send password reset**: emails the user a reset link on their behalf -
    the same flow the customer-facing "Forgot your password?" link uses.
  - Editing **your own account** disables the role and deactivation
    controls - you can't accidentally lock yourself out from here.

### Audit Log (`SuperAdmin`/`Admin` only)

- **Audit log** (`/Admin/AuditLog`): every recorded security-relevant
  event - logins, password changes/resets, lockouts, session revocations,
  and admin actions on other users' accounts - filterable by date range
  (defaults to the last 30 days), event type, outcome (succeeded/failed),
  and the acting user's email, showing the IP address recorded at the
  time. **Export CSV**.

### Settings (`SuperAdmin`/`Admin` only)

- **Store settings** (`/Admin/Settings`) - a single form, not a list,
  since there's exactly one store-wide configuration row:
  - **General**: store name, currency, default country (display-only).
  - **Pricing**: whether displayed prices include tax.
  - **Storefront**: how many items the recently-viewed section keeps.
  - **Checkout defaults**: the default tax/shipping country and region
    codes used for the Cart page's early tax/shipping estimates, before a
    real customer address is known (see Tax Rates/Shipping Methods above).
  - Two admins can't silently overwrite each other's changes to this
    shared row - saving detects the conflict and asks you to reload.

### Notifications

There's no admin UI for notifications - order-confirmation and
password-reset emails are sent automatically (customer-triggered, or
staff-triggered via **Send password reset** above) and aren't configured
or viewed from anywhere in the admin area.
