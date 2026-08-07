# Deployment Guide

## Status: complete (Milestone 18.3)

Target: **Windows Server + IIS**, framework-dependent deployment (no Docker,
no self-contained/single-file publish - `dotnet publish`'s default output
requires the .NET runtime installed on the host, same as local development).
Everything below was verified against a real publish output run outside
Visual Studio/`dotnet run` - published, launched under `ASPNETCORE_
ENVIRONMENT=Production` with secrets supplied only via environment
variables (no User Secrets, no `appsettings.Production.json`), migrated via
both documented methods, and hit on `/health/live`, `/health/ready`, and `/`
- not just written from the framework's general documentation.

## Prerequisites (on the target Windows Server)

- **.NET 10 Hosting Bundle** (not just the SDK or runtime alone) -
  installs the shared runtime *and* the ASP.NET Core Module (ANCM) IIS
  integrates against. Install it, then run `iisreset` once so IIS picks up
  the module.
- **IIS** with the Web Server role and the **ASP.NET Core Module V2**
  feature (installed by the Hosting Bundle above, not a separate IIS role
  feature to enable manually).
- **SQL Server** reachable from the app server (a named instance,
  `localhost`, or a remote server - not LocalDB, which is a per-user
  developer-only edition and was only ever used for local dev/CI in this
  project).
- A **TLS certificate** for the site's real hostname, installed into the
  server's certificate store (self-signed is fine for an internal-only
  deployment; anything customer-facing needs a certificate from a real CA
  or an ACME client like win-acme for Let's Encrypt).
- The `dotnet-ef` global tool **only** if you'll run migrations from the
  `dotnet ef database update` CLI (see "Migrations" below) - not required
  on the app server itself if you instead apply the generated SQL script.

## 1. Publish

```
dotnet publish src/ECommerceApp.Web/ECommerceApp.Web.csproj --configuration Release --output ./publish
```

Verified output (`./publish/`) includes `ECommerceApp.Web.dll` (the app,
launched via `dotnet ECommerceApp.Web.dll` - or by `.\ECommerceApp.Web.exe`
directly, an apphost wrapper around the same thing) and a **generated
`web.config`**:

```xml
<aspNetCore processPath="dotnet" arguments=".\ECommerceApp.Web.dll"
  stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout"
  hostingModel="inprocess" />
```

`hostingModel="inprocess"` means IIS's worker process (`w3wp.exe`) hosts
the app directly via ANCM, rather than IIS reverse-proxying to a separate
`dotnet` process on a loopback port - this is why "the app running under
IIS" and "the app running via `dotnet ECommerceApp.Web.dll` directly", the
way it was verified below, are a faithful match for everything except IIS
site bindings and app-pool-identity file permissions, which have no local
equivalent and are covered separately below. `web.config` is generated
automatically on every publish - don't hand-edit it; if a setting needs to
change (e.g. enabling `stdoutLogEnabled` for troubleshooting), set it via
`<PropertyGroup>` in the `.csproj` instead so it survives the next publish.

Copy the entire `./publish/` contents to the server (e.g.
`C:\inetpub\wwwroot\ECommerceApp\`). `appsettings.Development.json` is
harmless to leave in the publish output (it contains no secrets, only a
logging-level override that Production never reads - ASP.NET Core only
loads the `appsettings.{ASPNETCORE_ENVIRONMENT}.json` matching the actual
environment), but you can exclude it if you'd rather not ship a file that's
never used.

## 2. IIS site and application pool

1. Create an **application pool**: .NET CLR version **"No Managed Code"**
   (ANCM hosts the .NET runtime itself; IIS's own CLR hosting is for
   classic ASP.NET, not used here) - Managed pipeline mode doesn't matter,
   leave it Integrated.
2. Create a **site** (or application under an existing site) pointing its
   physical path at the folder from step 1, bound to the app pool from
   step 1a.
3. Add an **HTTPS binding** using the certificate from Prerequisites. Also
   keep an **HTTP binding** so `app.UseHttpsRedirection()` (already wired
   in `Program.cs`) has something to redirect *from* - IIS's ANCM
   automatically sets `ASPNETCORE_HTTPS_PORT` for the in-process app based
   on the site's HTTPS binding, so the redirect knows the right port without
   any extra configuration. (Verified the failure mode this prevents: run
   the app with only an HTTP endpoint bound and it logs `Failed to
   determine the https port for redirect` - harmless in that exact
   exposure, but confirms the HTTPS binding is what supplies this, not
   something the app guesses on its own.)
4. `app.UseHsts()` is already wired for non-Development environments -
   once the site is reachable over HTTPS, browsers that have loaded it once
   will refuse plain HTTP for the HSTS max-age window. Don't point real
   users at it over HTTP-only during initial testing if you're not ready
   for that yet.

## 3. Permissions

The app pool's identity (by default `IIS AppPool\<pool name>`) needs:

- **Read & execute** on the entire published folder (standard for serving
  the app + static assets under `wwwroot`).
- **Write** access to three specific subfolders the app creates and writes
  to at runtime - easy to miss if the site folder is otherwise locked down
  to read-only, which is a common IIS hardening step:
  - `wwwroot/uploads/` - product/variant images, brand logos, and home
    page banner images (`LocalFileStorage`, Milestone 2). Created
    automatically on first upload if missing, but the *parent* directory
    still needs to be writable for that to succeed. Without this, every
    admin image upload fails.
  - `Logs/` - Serilog's rolling file sink (`Logs/log-.txt`,
    `appsettings.json`'s `Serilog:WriteTo` section). Without this, the app
    still runs (file-sink write failures don't crash Serilog), but you
    lose the on-disk log history - only the Windows Event Log or IIS's own
    `stdout` log (if enabled in `web.config`) would show anything.
  - `DataProtection-Keys/` - the persisted key ring backing auth cookies
    and anti-forgery tokens (Milestone 17.2, see "Data protection" below).
    Without write access here, a *new* key ring is silently generated on
    every restart instead of reusing the persisted one, which has the same
    effect as if key persistence were never configured at all: every
    signed-in session and every open form with an anti-forgery token
    becomes invalid the moment the app pool recycles.

## 4. Secrets and configuration

**Never use User Secrets in production** - `dotnet user-secrets` stores
values in a plain-text JSON file under the *deploying developer's own user
profile* (`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`),
which doesn't exist on (and shouldn't be copied to) the server. Production
configuration instead comes from **environment variables**, using ASP.NET
Core's standard double-underscore syntax for nested keys - verified
end-to-end (published app, no `appsettings.Production.json`, only these
four environment variables set, `ASPNETCORE_ENVIRONMENT=Production`; the
app started clean and `/health/ready` reported healthy):

| Environment variable | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Jwt__Key` | Signs `/api/v1/auth` access tokens - a long, random value, different from any dev/test key |
| `SeedAdmin__Email` | First SuperAdmin account, seeded once at startup if the role has no members yet |
| `SeedAdmin__Password` | Must satisfy the same password policy as any other account (`Security.md`) |

Set these on the **application pool** (IIS Manager -> Application Pools ->
your pool -> Advanced Settings has no direct env-var UI in older IIS
versions; the reliable cross-version way is `system.webServer/aspNetCore/
environmentVariables` in `web.config`, or the app pool's own process
environment via `appcmd.exe`/PowerShell's `Set-WebConfigurationProperty`) -
not baked into `appsettings.json`, which is the same "never commit secrets"
rule dev/test already follow (`Security.md`'s "Secrets handling" section).
`appsettings.json`'s existing contents are already safe to ship as-is: it
only holds non-sensitive defaults (`Jwt:Issuer`/`Audience`, rate-limit
windows, the empty `SeedAdmin` placeholder overridden by the environment
variable above, etc.) - there is no separate `appsettings.Production.json`
in this repo, and none is needed, since nothing in Production's config
differs from the base file except the four secrets above.

Two more environment variables matter operationally, both optional:

- `DataProtection__KeyPath` - overrides where the persisted key ring lives
  (defaults to `DataProtection-Keys/` next to the app). Point this at a
  location outside the deployed-and-replaced app folder (see "Backup &
  rollback" below) if your deployment process wipes and replaces the app
  directory on every release.
- `Cors__AllowedOrigins` - a JSON array; leave unset (defaults to zero
  allowed origins) unless a separate origin (a mobile app, a standalone
  SPA) needs to call `/api/v1/auth` cross-origin.

## 5. Migrations

`Program.cs` deliberately does **not** auto-apply migrations at startup -
only the role/SuperAdmin and store-settings seeders run, and both log a
warning and let the app keep starting if the database isn't reachable or
migrated yet (so a first deploy against an unmigrated database doesn't
crash-loop; it just seeds nothing until you migrate and restart). Migrating
is a deploy-time step you run explicitly, before or right after copying the
published output. Two verified ways:

**Option A - idempotent SQL script (recommended for production)**, reviewable
by a DBA before running and requires no .NET tooling on the database server
itself:

```
dotnet ef migrations script --idempotent --project src/ECommerceApp.Infrastructure --startup-project src/ECommerceApp.Web --output migrate.sql
```

Run the generated `migrate.sql` against the target database with
`sqlcmd` or SQL Server Management Studio. `--idempotent` wraps each
migration in a check against `__EFMigrationsHistory`, so re-running the
same script on a database that's already partially or fully migrated is
safe - it skips whatever's already applied.

**Option B - direct CLI**, simpler for a first deploy or a small team
that already has `dotnet-ef` installed wherever they run this from:

```
dotnet ef database update --project src/ECommerceApp.Infrastructure --startup-project src/ECommerceApp.Web
```

This reads configuration the same way the app does, so pointing it at
production means setting `ConnectionStrings__DefaultConnection` (same
environment-variable name as above) in the shell you run it from - verified
this resolves correctly with no other configuration present, the same way
the running app resolves it.

Either way, run migrations **before** the first request hits a fresh
deploy if the release includes schema changes - the seeders tolerate a
stale schema by skipping and logging, but ordinary requests through
`ApplicationDbContext` will fail against missing tables/columns.

## 6. Data protection

Already configured in `Program.cs` (Milestone 17.2): keys persist to disk
(`DataProtection-Keys/` by default, or `DataProtection__KeyPath`) instead
of the framework's ephemeral in-memory default, so a restart or redeploy
doesn't invalidate every signed-in cookie and anti-forgery token. Two
things observed running the published app that are worth knowing before a
real deployment:

- The very first startup logs `No XML encryptor configured. Key ... may be
  persisted to storage in unencrypted form.` This is expected and
  documented behavior, not a bug - by default the key ring is protected by
  whatever the OS provides (Windows DPAPI on a single machine), which
  works for a **single-server** deployment but not a multi-server farm
  (each server's DPAPI-protected keys are unreadable by the others).
  Certificate- or Windows-DPAPI-NG-cluster-based key encryption is a real,
  deployment-target-specific decision (single box vs. farm, on-prem vs.
  cloud) intentionally left unmade here, matching the reasoning
  `Program.cs`'s own comment already gives for not wiring up encryption-at-
  rest without a chosen target. If you deploy to more than one server
  behind a load balancer, resolve this **before** going live (shared UNC
  path for `DataProtection__KeyPath` plus DPAPI-NG, or `.ProtectKeysWith
  Certificate(...)` with a certificate distributed to every server) -
  otherwise sessions become sticky-server-dependent in a way that looks
  like random logouts.
- **Back up `DataProtection-Keys/`** as part of your normal backup routine
  (see below) - losing it isn't data loss in the database sense, but it
  does mean every outstanding session and CSRF token goes invalid at once,
  which is a real (if recoverable) incident for anyone mid-checkout.

## 7. Health checks

- `GET /health/live` - always returns healthy if the process is up; used
  by IIS/a load balancer for "is the process alive" liveness probing.
- `GET /health/ready` - checks SQL Server reachability (`SqlServerHealthCheck`,
  bounded to a 5-second timeout since Milestone 17.3, so a hung database
  fails this fast instead of hanging the probe) - use this for readiness
  gating (e.g. don't route traffic to an instance still waiting on a
  database that isn't up yet).

Both are unauthenticated by design (a health probe can't present
credentials) and return no sensitive detail beyond healthy/unhealthy - no
additional exposure precaution is required, though restricting them to the
load balancer's IP at the IIS/firewall level is a reasonable belt-and-
suspenders step if you don't want them answerable from the public internet
at all.

## 8. Backup & rollback

- **Database**: standard SQL Server backup practice (full + transaction
  log backups per your RPO), outside this app's control - not a schema
  migration concern, a DBA/ops concern.
- **`DataProtection-Keys/`**: back up alongside the database (see "Data
  protection" above) - losing it is recoverable (users just have to sign
  in again) but avoidable.
- **`wwwroot/uploads/`**: back this up too - it's user-generated content
  (every product/variant image, brand logo, and banner an admin has
  uploaded) that exists only on disk, not in the database. A database
  restore without a matching `uploads/` restore leaves every image
  reference in the DB pointing at a file that no longer exists.
- **Rollback**: redeploy the previous release's published output over the
  current one, keeping `wwwroot/uploads/`, `Logs/`, and
  `DataProtection-Keys/` intact (don't let a "wipe and replace" deploy
  script delete these three). A **schema** rollback is not automatic -
  EF Core migrations in this project are additive/forward-only in
  practice; if a release's migration needs to be undone, that's a
  hand-written down-migration or a restore from the pre-migration backup,
  not something `dotnet ef` reverses for you by default. Plan the schema
  side of a rollback before you need it, not during an incident.

## 9. Post-deployment validation checklist

Run through this after every deploy, not just the first one:

1. `GET /health/live` and `GET /health/ready` both return healthy.
2. Log in as the seeded SuperAdmin (`SeedAdmin__Email`/`SeedAdmin__Password`)
   at `/Account/Login`, confirm the Admin dashboard loads at
   `/Admin/Home/Index` with real KPI cards (proves the database migrated
   and seeded correctly).
3. Upload an image somewhere in the admin area (a brand logo or product
   image is the fastest check) - confirms the `wwwroot/uploads/` write
   permission from step 3 is actually correct, not just assumed.
4. Place a full test order through the storefront (add to cart, checkout,
   use the documented test card numbers from `Customer-User-Guide.md`) -
   exercises the database, Data Protection-backed anti-forgery tokens, and
   the simulated payment gateway end-to-end in one pass.
5. Confirm the order-confirmation email was enqueued and processed (check
   `Logs/log-*.txt` for the outbox processor's activity, or - if no real
   SMTP sender is configured yet - the `DevEmailSender` preview file, per
   `README.md`).
6. `curl -I https://<your-host>/` and confirm the security headers from
   `Security.md` are present (`Content-Security-Policy`, `X-Frame-Options`,
   etc.) and that the response is HTTPS without a certificate warning.
7. Restart the app pool once, then repeat step 2's login - confirms
   `DataProtection-Keys/` persistence actually survived the restart rather
   than silently regenerating (a session that stays valid across the
   restart is the real proof; a session that gets silently logged out is
   the failure mode this specifically catches).

## Running locally (unchanged)

```
dotnet restore
dotnet build
dotnet run --project src/ECommerceApp.Web/ECommerceApp.Web.csproj
```

See `README.md` for local User Secrets setup - only for local development,
never for the production configuration described above.

No Docker is used anywhere in this project.
