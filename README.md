# ECommerceApp

A production-grade, single-store e-commerce application: customer storefront
+ admin panel, built with .NET, ASP.NET Core MVC, EF Core, and SQL Server.

Being built milestone-by-milestone; see [`docs/Milestone-Status.md`](docs/Milestone-Status.md)
for what's actually implemented right now versus still planned.

## Solution structure

```
ECommerceApp.sln
src/
  ECommerceApp.Domain/           entities, value objects, Result/Error - zero dependencies
  ECommerceApp.Application/      interfaces + application services - depends only on Domain
  ECommerceApp.Infrastructure/   EF Core, health checks - depends on Application + Domain
  ECommerceApp.Web/              MVC storefront/admin + /api/v1 Web API - the composition root
tests/
  ECommerceApp.Domain.Tests/
  ECommerceApp.Application.Tests/
  ECommerceApp.Infrastructure.Tests/
  ECommerceApp.Web.Tests/
  ECommerceApp.IntegrationTests/  architecture tests + WebApplicationFactory smoke tests
docs/
  Architecture.md, Database-Design.md, Data-Dictionary.md, Security.md,
  Application-Flow.md, Admin-User-Guide.md, Customer-User-Guide.md,
  Testing-Guide.md, Deployment-Guide.md, Milestone-Status.md
```

See [`docs/Architecture.md`](docs/Architecture.md) for the dependency rules
and the storefront/API integration decision.

## Important: target framework note

The project brief fixes the stack at **.NET 8**. This machine has only the
**.NET 10** SDK/runtime installed (no `net8.0` reference packs available), so
by agreement with the project owner the whole solution targets **`net10.0`**
instead. Every package version below follows from that. If you later install
the .NET 8 SDK and want to retarget, update `Directory.Build.props` and every
`.csproj`'s `<TargetFramework>`, then re-pin package versions to their 8.x
releases.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`dotnet --version` should report a `10.x` SDK)
- SQL Server (local instance, e.g. SQL Server Developer Edition or an existing
  named instance) - this project does not use Docker or an in-memory database
  for development/production
- The `dotnet-ef` global tool, for migrations:
  ```
  dotnet tool install --global dotnet-ef
  ```

## 1. SQL Server setup

Two development databases are used:

- `ECommerceAppDb` - normal development database
- `ECommerceAppTestDb` - used only by automated integration tests; never run
  tests against `ECommerceAppDb` or a production database

**Windows Authentication (default, recommended for local dev):**

```
Server=localhost;Database=ECommerceAppDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;
```

**SQL Authentication (alternative):**

```
Server=localhost;Database=ECommerceAppDb;User Id=YOUR_SQL_LOGIN;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True;
```

**LocalDB (if you don't have a full SQL Server instance)**: this is what was
used to develop and verify this milestone, since no plain `localhost` SQL
Server instance was available:

```
Server=(localdb)\MSSQLLocalDB;Database=ECommerceAppDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;
```

You don't need to create the database by hand - `dotnet ef database update`
(see below) creates it from migrations. For automated integration tests,
also create the empty test database once:

```
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "CREATE DATABASE ECommerceAppTestDb;"
```

(swap the `-S` server name for your own instance if not using LocalDB - the
integration tests apply migrations to it automatically, they just need the
database to exist).

## 2. Configure User Secrets

Sensitive configuration (connection string, JWT signing key, seed admin
password, email credentials) is **never** stored in `appsettings.json` - it's
kept in .NET User Secrets during development. From the repository root:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=ECommerceAppDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;" --project src/ECommerceApp.Web

dotnet user-secrets set "Jwt:Key" "REPLACE_WITH_A_LONG_RANDOM_SIGNING_KEY" --project src/ECommerceApp.Web

dotnet user-secrets set "SeedAdmin:Email" "admin@example.com" --project src/ECommerceApp.Web
dotnet user-secrets set "SeedAdmin:Password" "REPLACE_WITH_A_STRONG_PASSWORD" --project src/ECommerceApp.Web
```

All four are required as of Milestone 1: `Jwt:Key` signs API access tokens,
and `SeedAdmin:Email`/`SeedAdmin:Password` are what the app seeds your first
SuperAdmin login from on startup (skipped with a logged warning if either is
blank - there is no fallback admin account).

You can list what's configured with:

```
dotnet user-secrets list --project src/ECommerceApp.Web
```

## 3. Build, migrate, run, test

```
dotnet restore
dotnet build
```

Add a new migration when a milestone changes the schema, and apply pending
ones:

```
dotnet ef migrations add <Name> --project src/ECommerceApp.Infrastructure --startup-project src/ECommerceApp.Web
dotnet ef database update --project src/ECommerceApp.Infrastructure --startup-project src/ECommerceApp.Web
```

Run the app:

```
dotnet run --project src/ECommerceApp.Web/ECommerceApp.Web.csproj
```

Then check:
- `https://localhost:<port>/` - public storefront placeholder
- `https://localhost:<port>/Admin/Home/Index` - admin dashboard; requires a
  staff role (log in as the seeded SuperAdmin first, or you'll be redirected
  to log in / denied if you're a Customer)
- `https://localhost:<port>/health/live` and `/health/ready`
- `https://localhost:<port>/api/v1/auth/register` (POST JSON) and
  `/api/v1/auth/login` for the JWT API

Run all tests:

```
dotnet test
```

With code coverage:

```
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

See [`docs/Testing-Guide.md`](docs/Testing-Guide.md) for what each test
project covers and the test-database safety rules.

## Visual Studio

Open `ECommerceApp.sln`, set `ECommerceApp.Web` as the startup project, and
configure User Secrets via *Solution Explorer -> ECommerceApp.Web -> Manage
User Secrets* (same keys as step 2 above).

## Troubleshooting

- **App fails to start with "Connection string 'DefaultConnection' was not
  found"**: you haven't set the User Secret yet - see step 2.
- **`/health/ready` reports Unhealthy**: SQL Server isn't reachable, or the
  `ECommerceAppDb` database doesn't exist yet (it's created by `dotnet ef
  database update` once migrations exist). This does not affect `/health/live`.
- **`dotnet ef` command not found**: install the tool with `dotnet tool
  install --global dotnet-ef`.
- **`ECommerceApp.IntegrationTests` auth tests fail to connect**: they're
  hardcoded to `(localdb)\MSSQLLocalDB` / `ECommerceAppTestDb`
  (`tests/ECommerceApp.IntegrationTests/TestSupport/TestDatabase.cs`). If your
  machine uses a different SQL Server instance, update that connection string
  and make sure `ECommerceAppTestDb` exists (see step 1) - the tests apply
  migrations themselves but don't create the database.

## Notes

- No Docker, Angular/React/Vue/Blazor, in-memory production database,
  hardcoded secrets, or automatic source-control commits are used anywhere in
  this project.
