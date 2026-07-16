# Milestone Status

| Step | Status | Notes |
|---|---|---|
| Foundation Prompt | **Complete** | Clean-architecture scaffold, DI/EF Core base, common abstractions, cross-cutting middleware, health checks, base layouts, test projects + architecture tests, documentation. |
| Milestone 1 - Identity, Roles, MVC Login, JWT and Security | **Complete - pending review** | Identity (cookie) + JWT dual auth, roles/policies, register/login/logout/forgot/reset/change password, profile, revoke-all-sessions, `/api/v1/auth` endpoints, refresh-token rotation + reuse detection, account lockout, rate limiting, Admin Area now role-gated. See report for details. |
| Milestone 2 - Categories, Brands and Product Catalog Administration | Not started | |
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
