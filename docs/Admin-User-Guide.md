# Admin User Guide

## What you can do today (after Milestone 4.1)

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
    recorded but not yet enforced (the form says so); enforcing them needs
    order history, which doesn't exist until Milestone 9.
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

  These rates power the **Estimated tax** line on the customer-facing Cart
  page, calculated against the store's configured default jurisdiction
  (`Store:DefaultTaxCountryCode`/`Store:DefaultTaxRegionCode` in
  configuration) - not the customer's real shipping address, since there's
  no Address entity yet (that arrives in Milestone 8.1). The line only
  appears once at least one rate is configured; until then, nothing is
  shown rather than a misleading $0.00. Real, destination-based tax
  calculation at checkout arrives with Milestones 7.4/8.

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
  jurisdiction (`Store:DefaultShippingCountryCode`/
  `Store:DefaultShippingRegionCode`) powers the **Estimated shipping** line
  on the customer-facing Cart page - same estimate-only reasoning as tax
  rates (no real address yet, and no way to let the customer pick a method
  until checkout exists). The line only appears once at least one method
  is configured. Real, destination-based shipping calculation and method
  selection arrive with Milestones 7.4/8.

Orders, Customers, Reports, and Settings sections are still placeholders -
they activate in their respective milestones.

This guide grows section-by-section as each admin capability is built.
