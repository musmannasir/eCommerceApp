# Architecture

## Layers and dependency rules

```
ECommerceApp.Domain          no project references; no EF Core/MVC/SQL Server
ECommerceApp.Application     -> Domain only
ECommerceApp.Infrastructure  -> Application, Domain
ECommerceApp.Web             -> Application, Infrastructure
```

These rules are enforced by automated tests in
`tests/ECommerceApp.IntegrationTests/ArchitectureTests.cs`, which inspect each
compiled assembly's actual references (not just `.csproj` `ProjectReference`
entries) and fail the build if a lower layer starts depending on a higher one.

- **Domain** holds entities, value objects, and the `Result`/`Error` types.
  Zero third-party dependencies, so domain rules can be unit tested without a
  database, a web host, or mocks of infrastructure concerns.
- **Application** holds interfaces that Infrastructure/Web implement
  (`IClock`, `ICurrentUserService`, and per-milestone repository/service
  interfaces), plus FluentValidation validators and application services. No
  EF Core, no ASP.NET Core.
- **Infrastructure** holds `ApplicationDbContext`, EF Core configuration,
  health checks, and concrete implementations of Application interfaces that
  don't need the HTTP pipeline (e.g. `SystemClock`).
- **Web** is a single ASP.NET Core project containing both the MVC
  storefront/admin UI and the versioned Web API controllers under `/api/v1`.

## Storefront/API integration decision

The MVC controllers and the `/api/v1` Web API controllers both call
`Application`-layer services **in-process via dependency injection** - there
is no HTTP call from the MVC site to its own API. The two presentation layers
share one composition root (`Program.cs`) and one set of application
services; they differ only in how the caller authenticates:

- MVC: ASP.NET Core Identity cookie authentication + antiforgery tokens.
- API: JWT Bearer authentication (added in Milestone 1).

Rationale: looping the server-rendered site through its own HTTP API would
add latency and duplicate serialization for no benefit, since both run in the
same process. The `/api/v1` surface exists for external/future clients
(mobile apps, third parties), not as a backing service for the storefront.

## Identity placement (Milestone 1)

`ApplicationUser` (extends ASP.NET Core Identity's `IdentityUser`) lives in
**Infrastructure**, not Domain - Domain must stay free of any framework
dependency, and `IdentityUser` is a framework type. `RefreshToken`,
`UserSession`, and `SecurityAuditEvent` are plain Domain entities instead,
since they only need a `string UserId` reference, not an Identity base class.

Auth business logic (registration, login/lockout, token issuance/rotation,
audit logging) lives behind `IAuthService`, declared in **Application** as
primitive/DTO-typed methods, implemented by `AuthService` in
**Infrastructure** (where `UserManager`/`SignInManager` actually live).
Application never references `ApplicationUser` directly - this is what lets
`IAuthService` stay declared in Application without breaking the "Application
may reference only Domain" rule. Controllers (both MVC and API) depend on
`IAuthService`, not on Identity types, keeping them thin.

`SignInManager<ApplicationUser>` requires the full ASP.NET Core framework
(`IHttpContextAccessor`, authentication scheme provider), which a plain
classlib doesn't reference by default - `ECommerceApp.Infrastructure.csproj`
adds `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for this.
Web still owns the actual HTTP pipeline/middleware configuration.

## Catalog service pattern and file storage (Milestone 2)

`CategoryService`/`BrandService`/`ProductAttributeService`/`ProductService`
follow the same shape as `AuthService`: declared as interfaces in
Application, implemented in Infrastructure directly against
`ApplicationDbContext` (no separate repository layer - a repository
abstraction over EF Core, which is already a repository/unit-of-work
abstraction, would be indirection with no payoff here).

Image uploads go through `IFileStorage` (Application interface),
implemented by `LocalFileStorage` (Infrastructure) which writes to
`wwwroot/uploads/{category}/{random-guid}.{ext}`. The stored extension and
content-type are derived from the file's **signature** (magic bytes via
`ImageSignatureDetector`), never from the caller-supplied filename or
`Content-Type` header - both are trivially spoofable, and the brief requires
real content validation, not just extension checking. Filenames are always
random, and `DeleteAsync` only ever touches paths under `/uploads/`.

## A materialization bug this milestone's manual testing caught

`ProductService.AddVariantAsync` re-queried the just-created variant with
`.Select(v => MapVariant(v))` directly against `IQueryable<ProductVariant>`.
`MapVariant` is a plain C# method - EF Core can't translate it to SQL, and
without an explicit `.Include()` chain for
`AttributeValues.ProductAttributeValue.ProductAttribute`, those navigations
were null, throwing `NullReferenceException` (visible as a 500 from the
Admin UI). The `Infrastructure.Tests` unit test for this exact path (against
the EF Core InMemory provider) **passed anyway**, because the test reused one
`DbContext` across several calls in the same test, and EF's change-tracker
identity-fixup silently wired up the navigations from entities already
tracked earlier in the test - something a real, per-request `DbContext`
scope never benefits from. Fixed by explicitly `.Include()`-ing the chain
before calling `MapVariant`, and covered by a real end-to-end integration
test (`ProductAdminFlowTests`) that drives the actual Admin UI over HTTP
against the real SQL Server test database, not the InMemory harness.

Lesson generalized: a passing InMemory-backed unit test is not proof that a
projection or navigation chain works against a real relational provider -
prefer the real test database (or at least a fresh, untracked `DbContext`
per operation) for anything that depends on `.Include()` being right.

## Framework version note

The brief fixes the stack at **.NET 8**. This machine has only the **.NET 10**
SDK/runtime installed (confirmed by probing `dotnet --list-runtimes` and a
throwaway `net8.0` project, which failed to resolve `WebApplication` because
the net8.0 ASP.NET Core reference pack isn't present). Per the project owner's
decision, the entire solution targets **`net10.0`** instead, with EF Core,
ASP.NET Core Identity, and all other packages pinned to their net10.0-
compatible versions. No other architectural or scope decision in the brief is
affected by this substitution.

## Cross-cutting concerns

- **Auditing & soft delete**: entities derive from `AuditableEntity`
  (`Id`, `CreatedAtUtc`, `CreatedByUserId`, `UpdatedAtUtc`, `UpdatedByUserId`,
  `IsDeleted`, `RowVersion`). `ApplicationDbContext.SaveChanges[Async]`
  stamps the audit fields from `IClock`/`ICurrentUserService` and converts
  deletes of `ISoftDeletable` entities into an `IsDeleted = true` update.
  A global EF Core query filter hides soft-deleted rows automatically.
  Immutable financial/audit records must NOT implement `ISoftDeletable`.
- **Errors**: application code returns `Result`/`Result<T>` for expected
  failure paths instead of throwing. `Error` carries a machine-readable
  `ErrorType` (Validation, NotFound, Conflict, Unauthorized, Forbidden,
  Unexpected) that the Web layer maps to the right HTTP status.
- **Exception handling**: unhandled exceptions on `/api/*` are caught by
  `ApiExceptionHandler` and returned as RFC 7807 `ProblemDetails`; everything
  else falls back to the branded `/Home/Error` view. No exception details are
  ever shown to the user.
- **Correlation IDs**: `CorrelationIdMiddleware` assigns/propagates an
  `X-Correlation-Id` header and pushes it into the Serilog log context so all
  log lines for one request can be tied together.
- **Health checks**: `/health/live` reports process liveness only (no
  dependency checks). `/health/ready` additionally checks SQL Server
  reachability via `SqlServerHealthCheck`.

## Configuration resolution timing

Any configuration read that must reflect `WebApplicationFactory` test
overrides (connection strings, JWT settings, rate-limit thresholds) has to be
resolved **inside** a lazily-invoked delegate (an `AddDbContext`/`AddJwtBearer`
options callback, or a per-request rate-limiter partition function) rather
than captured into a local variable before `builder.Build()`. Test overrides
only merge into the final `IConfiguration` at `Build()` time, so an eager
read silently sees only the pre-test configuration. See `Security.md` for the
three places this actually broke Milestone 1's integration tests before being
fixed.

## Solution layout

See the root `README.md` for the full directory tree and setup instructions.
