# Admin User Guide

## What you can do today (after Milestone 2)

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

Inventory, Orders, Customers, Marketing, Reports, and Settings sections are
still placeholders - they activate in their respective milestones.

This guide grows section-by-section as each admin capability is built.
