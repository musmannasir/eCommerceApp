# Security

## Status after Milestone 1

Authentication and authorization are implemented. The Admin Area
(`/Admin/Home/Index`) now requires the caller to hold one of the non-Customer
roles (`Roles.StaffRolesCsv`); anonymous and Customer requests are correctly
denied (see `Testing-Guide.md` and the completion report for verification
details).

## Identity model

- **MVC**: ASP.NET Core Identity cookie authentication (`AddIdentity` in
  `Infrastructure/DependencyInjection.cs`). Cookie hardening is configured in
  `Web/Program.cs` via `ConfigureApplicationCookie`: `HttpOnly`, `Secure` in
  non-Development environments, `SameSite=Lax`, 14-day sliding expiration,
  `LoginPath=/Account/Login`, `AccessDeniedPath=/Home/AccessDenied`.
- **API**: JWT Bearer under `/api/v1/auth` (added alongside the cookie scheme
  via `AddAuthentication().AddJwtBearer(...)`, without changing the default
  scheme). Access tokens are short-lived (`Jwt:AccessTokenMinutes`, default
  15). Refresh tokens are opaque random values; only their SHA-256 hash is
  ever persisted (`RefreshToken.TokenHash`) - the raw value is returned to the
  caller once and never stored or logged.
- **Refresh-token rotation and reuse detection**: every successful refresh
  revokes the presented token and issues a new one
  (`AuthService.RefreshTokenAsync`). Presenting an already-revoked token is
  treated as reuse: the entire active token chain for that user is revoked
  immediately, forcing a fresh login.
- **Account lockout**: delegated to Identity's built-in
  `SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)` -
  5 failed attempts locks the account for 15 minutes. A separate,
  admin-controlled `ApplicationUser.IsActive` flag exists for permanently
  disabling an account (distinct from temporary lockout); both cases return
  the same generic "Invalid email or password" message on login to avoid
  distinguishing a disabled account from a wrong password. Locked-out
  accounts get their own explicit "temporarily locked" message.
- **Password policy**: minimum length 10, requires digit/lowercase/uppercase/
  non-alphanumeric, 4 required unique characters (`AddInfrastructure`).
- **Revoke-all-sessions**: revokes every active refresh token for the user
  (API/JWT sessions) and bumps the Identity security stamp via
  `UserManager.UpdateSecurityStampAsync` (cookie sessions - checked every 5
  minutes via a shortened `SecurityStampValidatorOptions.ValidationInterval`,
  rather than the 30-minute default). There is no separate live
  server-side session store to invalidate; `UserSession` is an audit trail,
  not an authorization check.
- **Roles**: `SuperAdmin`, `Admin`, `CatalogManager`, `InventoryManager`,
  `OrderManager`, `CustomerSupport`, `Customer` (`Domain.Security.Roles`).
  Seeded at startup by `RoleAndAdminSeeder`.
- **Policies**: `CanManageCatalog`, `CanManageInventory`, `CanManageOrders`,
  `CanManageUsers`, `CanViewFinancialReports`, `CanProcessRefunds`
  (`Domain.Security.Policies`), mapped to roles in `Web/Program.cs`. No
  controller currently enforces these individually (there's nothing to
  protect yet) - the Admin Area itself is gated by role membership instead;
  per-feature policy enforcement starts in Milestone 2.
- The first `SuperAdmin` is seeded exclusively from `SeedAdmin:Email` /
  `SeedAdmin:Password` (User Secrets in dev) by `RoleAndAdminSeeder`, run at
  startup. If either value is missing, seeding is skipped with a logged
  warning - no hardcoded fallback credentials exist anywhere in source.

## Rate limiting

Login, registration, forgot-password, and refresh endpoints are rate-limited
via the built-in ASP.NET Core rate limiter (`[EnableRateLimiting("auth")]`),
partitioned **per client IP** - an unpartitioned/global limiter would let one
abusive client exhaust the shared quota and lock out every other user, which
was caught and fixed during this milestone's own integration testing. The
permit limit and window are configurable (`RateLimiting:AuthPermitLimit`,
`RateLimiting:AuthWindowSeconds`; defaults 5 requests / 60 seconds) so test
environments can raise them without code changes.

## Open-redirect protection

`AccountController.RedirectToLocal` only redirects to `returnUrl` values that
pass `Url.IsLocalUrl`; anything else falls back to the home page and logs a
warning. Covered by an integration test using an external `returnUrl`.

## Secrets handling

- `appsettings.json` contains only non-sensitive defaults (`Jwt`, `SeedAdmin`,
  `Store`, `RateLimiting` sections - `SeedAdmin` and `Jwt:Key` ship empty).
- The SQL connection string, JWT signing key, and seed admin password are
  configured via **.NET User Secrets** in development (see README.md).
- `ApplicationDbContext`'s connection string, the JWT bearer's
  `TokenValidationParameters`, and the rate limiter's permit/window values are
  all resolved **lazily** (inside their respective configuration delegates,
  not eagerly before `WebApplicationBuilder.Build()`). This was a real bug
  found during this milestone: an eager read only sees configuration present
  before `Build()` completes, silently ignoring anything added afterward
  (including `WebApplicationFactory` test overrides) - the fix pattern is to
  defer the read into the lazy callback every one of these subsystems
  already provides.
- Dev password-reset emails are written to `Logs/DevEmails/*.html`
  (`DevEmailSender`) instead of a real send, and deliberately **not** through
  Serilog - so reset links/tokens never appear in the structured application
  log, only in the local preview file.

## Error handling

- `/api/*` unhandled exceptions return an RFC 7807 `ProblemDetails` response
  with a generic message and a correlation ID - never stack traces or
  internal details. Expected `Result`/`Error` failures from `IAuthService`
  are mapped to the matching HTTP status via `ResultExtensions.ToProblem`.
- All other unhandled exceptions redirect to the branded `/Home/Error` view
  in non-Development environments; `app.UseDeveloperExceptionPage()` only in
  Development.

## Transport security

- `app.UseHsts()` and `app.UseHttpsRedirection()` are enabled; HSTS only
  applies outside Development, matching ASP.NET Core defaults.

## Test-process note

`WebApplicationFactory<Program>`-based integration tests all boot the same
`Program.cs`, which uses Serilog's shared static `Log.Logger` and closes it
in a `finally` block. Running multiple factories concurrently (xUnit's
default across test collections) can have one factory's shutdown tear down
the logger while another is mid-startup. `ECommerceApp.IntegrationTests` sets
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` to avoid
this - a real flake this milestone's testing turned up, not a hypothetical.

## What's still deliberately not built

Content-Security-Policy and other security response headers, and CORS
configuration, belong to Milestone 17 (hardening pass).
