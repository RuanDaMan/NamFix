# NamFix — engineering rules for Claude

> Read `CONTEXT.md` first for architecture. This file holds the hard rules that must be followed on
> every change. Rules here override default behaviour.

## Error handling & logging (MANDATORY)

**Every single HTTP request must handle its errors and surface them to the client.** This applies to
both sides of the wire:

- **API (server):** No request may fail silently or leak a raw stack trace to the user. Unhandled
  exceptions are caught by the global exception handler (`NamFix.Api/Infrastructure/GlobalExceptionHandler.cs`),
  which **logs the full exception** (Serilog) and returns a short, safe message to the client as an
  `ErrorResponse` (`NamFix.Shared.Dtos.ErrorResponse` — `{ error, traceId }`). Expected/validation
  failures should return the same `ErrorResponse` shape with an appropriate status code.

- **Client (Blazor):** Every call in `NamFixApiClient` must go through the central `SendAsync`
  helpers. On any non-success response or transport failure (server offline), the client **logs the
  full detail** and **shows the user at least the short message** via `ApiErrorNotifier` (rendered as
  a toast in `MainLayout`). Never swallow an HTTP error and return `null`/empty without notifying.

Rule of thumb: **the short message is always displayed to the user; the full detail is always
logged.** Do not add an HTTP call that violates this.

## Logging

- Use **Serilog** for all server-side logging (configured in `NamFix.Api/Program.cs`). Logs go to
  console and a rolling file under `NamFix.Api/logs/`. Do not use `Console.WriteLine` or the default
  logger directly.

## Startup / health

- **The API must not start if it cannot reach the database.** A startup connectivity check runs
  before the host listens; on failure it logs a fatal error via Serilog and exits non-zero. Do not
  bypass this check.

## Connectivity

- The client tracks backend availability over **SignalR** (`/hubs/status`, `StatusHub`). The
  `ConnectivityService` exposes online/offline state; the nav shows a status indicator and an offline
  overlay appears when the connection drops, auto-clearing when it returns. Keep this working when
  changing startup or hosting.
