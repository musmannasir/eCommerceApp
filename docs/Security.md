# Security

## Status after Milestone 2

Catalog administration (Categories, Brands, Products, Product Attributes) is
gated by the `CanManageCatalog` policy (`SuperAdmin`/`Admin`/`CatalogManager`
roles) - anonymous requests redirect to login, authenticated non-catalog
roles (e.g. `Customer`) get a 403 Access Denied, verified by
`CatalogAuthorizationTests`.

## File upload security (Milestone 2)

- Allowed types: JPEG, PNG, WebP - determined from the file's **signature**
  (first bytes), never the client-supplied filename extension or
  `Content-Type` header, both of which are trivial to spoof. See
  `ImageSignatureDetector` and `Architecture.md`.
- Size limit is configurable (`FileStorage:MaxImageSizeBytes`, default 5 MB)
  and enforced by counting bytes while copying, not by trusting a
  `Content-Length` header.
- Stored filenames are always a random GUID plus the *detected* extension -
  the original filename is never used to construct a path, eliminating
  directory traversal via a crafted filename.
- `IFileStorage.DeleteAsync` refuses to touch any path outside `/uploads/`.
- Orphaned files (an image record deleted from the DB) are deleted from disk
  in the same operation (`ProductService.DeleteImageAsync`).

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

## Security response headers (Milestone 17.1)

Every response - MVC pages, `/api/*`, error/status-code pages - carries a
fixed set of hardening headers, set by `SecurityHeadersMiddleware` (the
first middleware in the pipeline, so it runs regardless of what happens
downstream):

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=(), usb=()`
- `Content-Security-Policy`: `default-src 'self'; script-src 'self'
  'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;
  font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self';
  frame-ancestors 'none'`

**`script-src`/`style-src` keep `'unsafe-inline'` - a deliberate, narrower
scope than a fully strict CSP.** An audit before implementing found ~25 view
files using inline `<script>` blocks, `onsubmit`/`onclick`/`onchange`
attributes (including nearly every admin delete-confirmation dialog), or
inline `style="..."`. Eliminating `'unsafe-inline'` via nonces is possible
for `<script>` content but has no equivalent for `style="..."` attributes
(CSP has no nonce mechanism for attribute-level styles), so removing it
fully would mean rewriting ~25 behavior-sensitive views (including every
delete confirmation) into external/nonce'd scripts and converting ~74
inline `style="..."` occurrences to CSS classes - real, non-trivial work
better scoped as its own follow-up than folded into a headers middleware
milestone. The other directives (`object-src 'none'`, `base-uri 'self'`,
`form-action 'self'`, `frame-ancestors 'none'`, `default-src 'self'`) still
meaningfully restrict what an injected payload could do - in particular,
`default-src`/`img-src` block exfiltration via a `<img src="https://evil...">`-
style payload even without script execution, and `frame-ancestors 'none'`
is the modern replacement for `X-Frame-Options` against clickjacking. `img-src`
includes `data:` because Bootstrap's own CSS embeds `data:image/svg+xml`
URIs for form-select carets and similar icons - discovered as a real
regression during manual verification, not anticipated in advance.

## CORS (Milestone 17.1)

No cross-origin caller is evidenced anywhere in this codebase - the
storefront's own JS only ever calls same-origin endpoints. Rather than leave
CORS entirely unconfigured (an accidental "deny all" that a future
`AddCors()` call elsewhere could silently change), there's now an explicit
default policy (`Program.cs`) sourced from `Cors:AllowedOrigins`
(`appsettings.json`, empty array by default = zero origins allowed). A
future cross-origin consumer (a separate SPA, a mobile app calling
`/api/v1/auth`) opts in by listing its origin in configuration - no code
change needed.
