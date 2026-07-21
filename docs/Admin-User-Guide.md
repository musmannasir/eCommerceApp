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

Orders, Customers, Reports, and Settings sections are still placeholders -
they activate in their respective milestones. Promotions and coupons
(Milestone 7) will join Marketing alongside home page banners.

This guide grows section-by-section as each admin capability is built.
