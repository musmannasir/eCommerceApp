# Milestone Status

| Step | Status | Notes |
|---|---|---|
| Foundation Prompt | **Complete** | Clean-architecture scaffold, DI/EF Core base, common abstractions, cross-cutting middleware, health checks, base layouts, test projects + architecture tests, documentation. |
| Milestone 1 - Identity, Roles, MVC Login, JWT and Security | **Complete** | Identity (cookie) + JWT dual auth, roles/policies, register/login/logout/forgot/reset/change password, profile, revoke-all-sessions, `/api/v1/auth` endpoints, refresh-token rotation + reuse detection, account lockout, rate limiting, Admin Area now role-gated. |
| Milestone 2 - Categories, Brands and Product Catalog Administration | **Complete - pending review** | Categories (tree, unlimited nesting, circular-reference protection), Brands (+ logo upload), Product Attributes/Values, Products (variants, images, specifications, tags, SEO, publish workflow), soft delete + recycle bin throughout, real RowVersion concurrency, secure image upload (signature-validated). See report for details. |
| Milestone 3 - Inventory, Warehouses, Suppliers and Purchase Orders | Not started | |
| Milestone 4 - Storefront Home, Navigation and Product Discovery | Not started | |
| Milestone 5 - Product Detail, Pricing, Variants and Recommendations | Not started | |
| Milestone 6 - Shopping Cart, Guest Cart and Wishlist | Not started | |
| Milestone 7 - Promotions, Coupons, Tax and Shipping | Not started | |
| Milestone 8 - Customer Addresses and Checkout | Not started | |
| Milestone 9 - Orders, Payments and Stock Reservation | Not started | |
| Milestone 10 - Admin Order Queue, Processing and Shipment Management | Not started | |
| Milestone 11 - Customer Account, Order History and Tracking | Not started | |
| Milestone 12 - Product Reviews, Ratings and Moderation | Not started | |
| Milestone 13 - Cancellation, Returns and Refunds | Not started | |
| Milestone 14 - Financial Dashboard, Cash Flow and Reports | Not started | |
| Milestone 15 - Notifications, Outbox and Background Processing | Not started | |
| Milestone 16 - User Management, Audit Logs and Store Configuration | Not started | |
| Milestone 17 - Security Hardening, Performance and Reliability | Not started | |
| Milestone 18 - Complete Testing, Documentation and Production Deployment | Not started | |

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

## Known EF Core warnings (benign, not yet addressed)

At startup: `Product` and `ProductAttributeValue` have global (soft-delete)
query filters and are the *required* end of a relationship with
`ProductTagMapping` and `ProductVariantAttributeValue` respectively. This is
cosmetic for how the app actually queries (always parent-down, never
join-table-up), but is left as a known warning rather than silently
suppressed - worth a proper look if a future milestone ever queries those
join tables directly as roots.
