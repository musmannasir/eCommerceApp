# Admin User Guide

## What you can do today (after Milestone 1)

- **Log in as the seeded SuperAdmin**: configure `SeedAdmin:Email` /
  `SeedAdmin:Password` via User Secrets (see README.md), start the app once
  so it seeds the account, then log in at `/Account/Login` with those
  credentials.
- **Access the Admin dashboard**: `/Admin/Home/Index`, reachable by any
  staff role (`SuperAdmin`, `Admin`, `CatalogManager`, `InventoryManager`,
  `OrderManager`, `CustomerSupport`). Customers are redirected to an
  "Access denied" page if they try.
- The dashboard itself is still a placeholder - Catalog, Inventory, Orders,
  Customers, Marketing, Reports, and Settings sections activate in their
  respective milestones (starting with Catalog in Milestone 2).

This guide grows section-by-section as each admin capability is built.
