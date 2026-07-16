# Data Dictionary

Tables added by the `InitialIdentityAndSecurity` migration (Milestone 1).
ASP.NET Core Identity's own tables (`AspNetUsers`, `AspNetRoles`,
`AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`,
`AspNetUserTokens`, `AspNetRoleClaims`) follow the framework's standard
schema plus the extra columns listed below; only the application-specific
tables are fully documented column-by-column.

## AspNetUsers (extended)

In addition to Identity's standard columns (`Id`, `UserName`,
`NormalizedUserName`, `Email`, `NormalizedEmail`, `EmailConfirmed`,
`PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `PhoneNumber`,
`PhoneNumberConfirmed`, `TwoFactorEnabled`, `LockoutEnd`, `LockoutEnabled`,
`AccessFailedCount`):

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| FirstName | nvarchar(100) | No | Profile field |
| LastName | nvarchar(100) | No | Profile field |
| IsActive | bit | No | Admin-controlled permanent disable, separate from temporary lockout |
| CreatedAtUtc | datetime2 | No | Account creation timestamp (UTC) |
| LastSuccessfulLoginAtUtc | datetime2 | Yes | Updated on every successful login |
| PasswordChangedAtUtc | datetime2 | Yes | Updated on registration, change, and reset |

A unique index on `NormalizedEmail` enforces one account per email
(`options.User.RequireUniqueEmail = true` plus an explicit unique index,
since Identity only indexes `NormalizedUserName` by default).

## RefreshTokens

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| UserId | nvarchar(450) | No | FK to AspNetUsers.Id (indexed) |
| TokenHash | nvarchar(256) | No | SHA-256 hash of the raw token; unique index. Raw value is never stored |
| ExpiresAtUtc | datetime2 | No | |
| CreatedAtUtc | datetime2 | No | |
| CreatedByIp | nvarchar(64) | Yes | |
| RevokedAtUtc | datetime2 | Yes | Null while active |
| RevokedByIp | nvarchar(64) | Yes | |
| ReplacedByTokenHash | nvarchar(max) | Yes | Set when rotated, forming the reuse-detection chain |
| ReasonRevoked | nvarchar(200) | Yes | `"rotated"`, `"logout"`, `"reuse_detected"`, `"revoke_all_sessions"`, `"password_changed"`, `"password_reset"` |

## UserSessions

Lightweight login audit trail - not consulted to authorize requests (see
`Security.md`).

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| UserId | nvarchar(450) | No | FK to AspNetUsers.Id (indexed) |
| LoginMethod | int (enum) | No | `CookieMvc` or `JwtApi` |
| LoginAtUtc | datetime2 | No | |
| IpAddress | nvarchar(64) | Yes | |
| UserAgent | nvarchar(512) | Yes | |
| LoggedOutAtUtc | datetime2 | Yes | |
| IsRevoked | bit | No | Set by `RevokeAllSessionsAsync` |

## SecurityAuditEvents

Immutable security audit log - never edited or soft-deleted; corrections are
new rows, not mutations.

| Column | Type | Nullable | Purpose |
|---|---|---|---|
| Id | int (identity) | No | Surrogate key |
| UserId | nvarchar(450) | Yes | Null when the event has no matching account (e.g. login attempt with an unknown email) |
| EventType | int (enum) | No | See `Domain.Security.SecurityEventType` |
| OccurredAtUtc | datetime2 | No | Indexed |
| Succeeded | bit | No | |
| IpAddress | nvarchar(64) | Yes | |
| UserAgent | nvarchar(512) | Yes | |
| CorrelationId | nvarchar(max) | Yes | Ties the event to the request's `X-Correlation-Id` |
| Details | nvarchar(1000) | Yes | Safe, non-sensitive summary - never a password or token |
