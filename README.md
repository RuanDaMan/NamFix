# NamFix 🔧

A tradesperson directory & booking platform for Namibia. Clients find, contact, and pay skilled
providers (plumbers, electricians, mechanics, carpenters, …) across all major towns. The platform
earns a **commission on each transaction** processed through it.

Built so a **.NET MAUI mobile app can reuse the Blazor UI** later with minimal rework.

> 📖 **`CONTEXT.md` is the canonical architecture document** — read it first. This README is the
> quick-start.

---

## Stack

Blazor WebAssembly (.NET 10) · ASP.NET Core Web API · **Dapper** (no EF) · **SQL Server** with
full-text search · **Grate** migrations · JWT auth · **Leaflet.js** maps.

## Projects

| Project            | What it is                                                              |
|--------------------|------------------------------------------------------------------------|
| `NamFix.Shared`    | DTOs, domain models, enums, abstraction interfaces.                     |
| `NamFix.Application`| Business logic + Dapper repositories + services + JWT/commission.     |
| `NamFix.SharedUi`  | All reusable Blazor UI (pages, components, API client, map) — an RCL.   |
| `NamFix.Web`       | Thin Blazor WASM host.                                                  |
| `NamFix.Api`       | Web API **and** the host that serves the WASM client.                  |
| `NamFix.Mobile`    | Future MAUI Blazor Hybrid — scaffold/docs only (see its README).        |

The API and WASM client ship together: `NamFix.Api` references `NamFix.Web` and serves it.

## Prerequisites

- .NET 10 SDK
- SQL Server (LocalDB, Express, or full)
- Grate: `dotnet tool install -g grate`

## Quick start

```bash
# 1. Build
dotnet build NamFix.sln

# 2. Create schema + seed data (NOTE: do not pass --transaction; see db/README.md)
grate --connectionstring="Server=localhost;Database=NamFix;Trusted_Connection=True;TrustServerCertificate=True;" \
      --sqlfilesdirectory=./db --databasetype=sqlserver \
      --folders='{"runAfterCreateDatabase":{"type":"Once"},"up":{"type":"Once"},"views":{"type":"AnyTime"},"sprocs":{"type":"AnyTime"},"permissions":{"type":"EveryTime"},"seed":{"type":"EveryTime"}}'

# 3. Run (the API also serves the Blazor WASM front-end)
dotnet run --project NamFix.Api
```

Open the API's HTTPS URL in a browser. The OpenAPI document is at `/openapi/v1.json` in Development.

### Sample logins (password `Password123!`)

| Email             | Role             |
|-------------------|------------------|
| `admin@namfix.na` | Admin            |
| `aqua@namfix.na`  | Service Provider |
| `spark@namfix.na` | Service Provider |
| `auto@namfix.na`  | Service Provider |

Register your own **Client** account to search, contact via WhatsApp, pay through the platform, and
leave reviews.

## Configuration

`NamFix.Api/appsettings.json`:

- `ConnectionStrings:NamFix` — SQL Server connection string.
- `Jwt:SigningKey` — **change in production** (min 32 chars). Use a secret/env var.
- `Cors:Origins` — allowed origins when running the WASM client on a different port.

Any setting can be overridden with a `NAMFIX_`-prefixed environment variable (double underscore for
nested keys), e.g.:

```bash
NAMFIX_ConnectionStrings__NamFix="Server=...;Database=NamFix;..."
NAMFIX_Jwt__SigningKey="a-long-random-production-secret"
```

## Database migrations

See **`db/README.md`** for the folder layout, run order, and CI command.

## Key features

Role-based auth (Client / Provider / Admin) · provider onboarding with map pin, service towns,
moderated tags, availability & emergency flag · full-text + filtered + "near me" search ·
Leaflet maps · WhatsApp deep-link contact · reviews with verified-transaction flag ·
**transaction commission** flow (hold → payout) with admin-configurable rates ·
provider earnings & admin revenue reporting · mobile-responsive from day one.
