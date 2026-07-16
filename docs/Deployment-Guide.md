# Deployment Guide

## Status after the Foundation milestone

The full Windows IIS deployment guide (hosting bundle, app pool, permissions,
connection strings, environment variables, Data Protection key directory,
image/log directories, HTTPS certificate, secrets, migrations, health
checks, backup/rollback, post-deployment validation) is written as part of
**Milestone 18**, once there is a complete application to deploy.

## Running locally today

```
dotnet restore
dotnet build
dotnet run --project src/ECommerceApp.Web/ECommerceApp.Web.csproj
```

See `README.md` for User Secrets setup (required before the app can start,
since it needs a SQL Server connection string).

## Publish command (documented now, exercised fully at Milestone 18)

```
dotnet publish src/ECommerceApp.Web/ECommerceApp.Web.csproj --configuration Release --output .\publish
```

No Docker is used anywhere in this project.
