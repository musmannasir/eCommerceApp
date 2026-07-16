# Testing Guide

## Test projects

| Project | Purpose |
|---|---|
| `tests/ECommerceApp.Domain.Tests` | Pure unit tests for domain entities, value objects, and `Result`/`Error`. No I/O. |
| `tests/ECommerceApp.Application.Tests` | Unit tests for FluentValidation validators (`RegisterRequestValidator`, `ChangePasswordRequestValidator`, etc.). |
| `tests/ECommerceApp.Infrastructure.Tests` | EF Core behavior tests (InMemory provider) plus `AuthService` tests against a real Identity stack (`UserManager`/`SignInManager`/`RoleManager`) backed by InMemory - see `AuthServiceTestHarness`. Covers registration, login/lockout, refresh-token rotation/expiry/revocation/reuse, logout, revoke-all-sessions, forgot/reset/change password. |
| `tests/ECommerceApp.Web.Tests` | Unit tests for controllers, services, and middleware in isolation (no real HTTP host). |
| `tests/ECommerceApp.IntegrationTests` | Architecture/dependency-rule tests, full-pipeline smoke tests, and (from Milestone 1) full-stack auth tests against a real SQL Server test database via `WebApplicationFactory<Program>` - admin-area authorization, open-redirect protection, the JWT API surface, and the MVC change-password flow. |

## Running tests

```
dotnet test
```

With coverage (Coverlet, Cobertura format under `TestResults/`):

```
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

## Test database safety

- Automated tests must never run against `ECommerceAppDb` (development) or
  any production database. **`ECommerceAppTestDb`** (on the local LocalDB
  instance, `(localdb)\MSSQLLocalDB`) is used by
  `ECommerceApp.IntegrationTests`'s auth tests (`TestSupport/TestDatabase.cs`,
  `AuthWebApplicationFactory`), which apply migrations and wipe the
  auth-related tables once per test collection
  (`AuthTestFixture.InitializeAsync`) for a clean, deterministic slate.
- The Foundation-level smoke tests in `ApplicationStartupTests` still don't
  need a reachable database at all: they override
  `ConnectionStrings:DefaultConnection` with a placeholder purely to satisfy
  DI service registration.
- From Milestone 18 onward, the full test-database bootstrap must also
  actively reject any connection string that looks like the dev or
  production database name.

## A real bug this milestone's tests caught

Running the full `ECommerceApp.IntegrationTests` suite together (not just one
test at a time) surfaced two real issues that a narrower test run would have
missed:

1. The rate limiter was **unpartitioned** (one global counter for all
   clients), so the test suite's own traffic exhausted the login/register
   quota within seconds - the same bug would let one abusive client in
   production lock out every other user. Fixed by partitioning per client IP.
2. Several `IConfiguration` reads (JWT bearer options, the rate limiter's
   permit limit, originally the DB connection string) were captured **before**
   `WebApplicationBuilder.Build()`, so they never saw `WebApplicationFactory`
   test overrides (which only merge in at `Build()` time) - see
   `Architecture.md`'s "Configuration resolution timing" section.

Moral: a feature that only gets exercised by one isolated test, or only ever
tested via `WebApplicationFactory` with its defaults, can hide bugs that only
show up under the conditions the feature exists for (concurrent real users,
or genuinely varying configuration). Prefer running the whole suite, not
just the new tests, before calling a milestone done.

## Test-process parallelism

`ECommerceApp.IntegrationTests` sets
`[assembly: CollectionBehavior(DisableTestParallelization = true)]`
(`AssemblyInfo.cs`) because every `WebApplicationFactory<Program>` instance
boots the same `Program.cs`, which uses Serilog's shared static `Log.Logger`
and closes it in a `finally` block - concurrent factories (xUnit's default
across collections) could have one factory's shutdown tear down the logger
mid-startup for another. This was an actual observed flake, not a
precaution.

## Architecture tests

`ArchitectureTests.cs` inspects each compiled assembly's real references
(`Assembly.GetReferencedAssemblies()`), not just `.csproj` `ProjectReference`
entries, and fails if a dependency-rule violation is introduced (e.g. if
`Domain` ever starts referencing EF Core, or `Application` starts
referencing `Infrastructure`).

## Coverage targets

Per-milestone coverage isn't gated numerically until Milestone 18, which sets
the project-wide targets (>=80% Application-layer business logic, >=70%
overall meaningful line coverage). Earlier milestones should still cover
every new business rule; padding with meaningless tests is explicitly out of
scope.
