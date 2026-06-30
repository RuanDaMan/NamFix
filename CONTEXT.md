# NamFix — Canonical Context

> **Read this file first.** It is the single source of truth for the architecture so any developer
> or AI assistant can be brought up to speed from one document. Keep it current as things change.

NamFix is a tradesperson directory & booking platform for Namibia. Clients find and pay skilled
providers (plumbers, electricians, mechanics, …); the platform earns a **commission on each
transaction** processed through it.

---

## 1. Solution structure & responsibilities

```
NamFix.sln
├── NamFix.Shared        Class lib  — DTOs, domain models, enums, contracts/interfaces. No dependencies.
├── NamFix.Application   Class lib  — business logic + Dapper data access. Depends on Shared.
├── NamFix.SharedUi      Razor RCL  — ALL reusable Blazor UI (pages, layout, components, API client,
│                                     map/geolocation). Depends on Shared.
├── NamFix.Web           Blazor WASM— standalone front-end host. Depends on SharedUi + Shared.
├── NamFix.Api           ASP.NET API— Web API backend (REST + SignalR) only. Depends on Application,
│                                     Shared. Does NOT reference or host Web.
└── NamFix.Mobile        (scaffold) — future .NET MAUI Blazor Hybrid. NOT in the solution yet; see its README.
```

### Dependency direction (arrows point to the dependency)

```
NamFix.Api  ──►  NamFix.Application  ──►  NamFix.Shared
                                             ▲
        NamFix.Web  ──►  NamFix.SharedUi  ───┘
```

`Shared` is the leaf everything points at. `Api` and `Web` are fully decoupled — they share no
project reference. UI never references `Application`; it only talks to the API over HTTP via
`NamFixApiClient`.

> **API and client are separate deployables.** `NamFix.Api` is a pure backend (REST + SignalR); it
> does not host the client. `NamFix.Web` is served on its own (its dev server locally; a static host
> in production) and reaches the API cross-origin via `ApiBaseUrl` (`wwwroot/appsettings.json`), so
> the API's `Cors:Origins` allowlist is load-bearing — keep it in sync with where the client runs.

---

## 2. Tech stack

| Concern      | Choice                                                                 |
|--------------|------------------------------------------------------------------------|
| Frontend     | Blazor WebAssembly (.NET 10), components in an RCL for MAUI reuse       |
| Backend      | ASP.NET Core Web API (controllers), layered API → Application → Data    |
| Data access  | **Dapper** (no EF). Hand-written SQL in a repository layer.            |
| Database     | Microsoft SQL Server, with **full-text search** (FREETEXT/CONTAINS)    |
| Migrations   | **Grate** — see `NamFix.Api/Migrations/README.md`                       |
| Auth         | Custom token store (Dapper) + **JWT** access/refresh tokens             |
| Maps         | **Leaflet.js** via JS interop, behind `IMapService`                     |
| Styling      | Plain responsive CSS (`namfix.css` in the RCL), mobile-first           |

---

## 3. Database conventions

SQL lives in `NamFix.Api/Migrations/` and is run by Grate. See `NamFix.Api/Migrations/README.md` for the run order table and exact command.

- **`Migrations/up/`** — versioned, run-**once** schema. Files are sequentially numbered
  (`0001_create_users.sql`, …). Tables, indexes, FKs, and the full-text catalog/index live here.
- **`Migrations/views/`** — `CREATE OR ALTER VIEW`, re-run any time the file changes.
- **`Migrations/sprocs/`** — `CREATE OR ALTER PROCEDURE`, re-run any time changed (e.g. `usp_ProviderTypeahead`).
- **`Migrations/permissions/`** — grants, run every time (guarded/idempotent).
- **`Migrations/seed/`** — reference + sample data, run every time, **idempotent** (`MERGE` / `IF NOT EXISTS`).
- **`Migrations/runAfterCreateDatabase/`** — one-time setup right after the DB is created.

**Naming:** PascalCase tables/columns; PK `PK_<Table>`, FK `FK_<Table>_<Ref>`, unique `UX_…`, index `IX_…`.
Enums are stored as `INT` columns (see `NamFix.Shared.Enums`).

**Full-text setup:** catalog `NamFixCatalog`; full-text index on
`dbo.Providers(BusinessName, Description, SearchKeywords)` keyed by `PK_Providers`. `SearchKeywords`
is a **denormalized blob** (business name + category + tags) maintained by `ProviderService` on save,
so tag/category terms are searchable without joins. Search uses `FREETEXT` for natural-language,
inflection-tolerant matching; the typeahead sproc uses `CONTAINS` with a prefix term.

---

## 4. Auth approach

- Registration picks a role (`Client`, `ServiceProvider`, `Admin`) up front (low-friction onboarding).
- Passwords hashed with **PBKDF2-SHA256** (`PasswordHasher`), format `{iterations}.{salt}.{key}`.
- Login/refresh issue a **JWT access token** (short-lived) + an opaque **refresh token** (DB-stored,
  rotated on use, revocable). See `AuthService` and `JwtTokenService`.
- The API validates JWTs via `AddJwtBearer`; controllers authorize by role
  (`[Authorize(Roles = nameof(UserRole.Admin))]`, etc.).
- **Token storage is behind `ITokenStore`** — web uses `localStorage` (`LocalStorageTokenStore`),
  MAUI will use SecureStorage. The Blazor auth state is rebuilt from the JWT by
  `NamFixAuthStateProvider`; `AuthHeaderHandler` attaches the bearer token to API calls.
- The signing key may come from config or a `NAMFIX_`-prefixed env var (e.g. `NAMFIX_Jwt__SigningKey`).
  The **database connection string is read exclusively from `appsettings.json`**
  (`ConnectionStrings:DefaultConnection`) and cannot be overridden by an environment variable.

---

## 5. UI-sharing strategy (web now, MAUI later)

- **Every routable page, layout, and component lives in `NamFix.SharedUi`** (a Razor Class Library),
  along with the typed `NamFixApiClient`, auth state provider, and map/geolocation services.
- `NamFix.Web` is a thin host: its `App.razor` pulls the RCL routes in via
  `AdditionalAssemblies`, and `index.html` loads Leaflet + the RCL's `namfix.css`.
- The future `NamFix.Mobile` (MAUI Blazor Hybrid) renders the same RCL inside a `BlazorWebView`.
  See `NamFix.Mobile/README.md` for the exact wiring. Only platform-specific implementations differ.

---

## 6. Platform abstractions (in `NamFix.Shared.Contracts`)

| Interface              | Web implementation (where)                         | MAUI implementation (later) |
|------------------------|----------------------------------------------------|------------------------------|
| `IMapService`          | `LeafletMapService` (SharedUi, JS interop)         | reused as-is (BlazorWebView) |
| `IGeolocationService`  | `BrowserGeolocationService` (SharedUi)             | reused as-is                 |
| `ITokenStore`          | `LocalStorageTokenStore` (**NamFix.Web**)          | SecureStorage-backed         |
| `ISecureStorage`       | (not yet needed)                                   | MAUI SecureStorage           |
| `IPaymentService`      | `StubPaymentService` (**NamFix.Application**)       | DPO / PayToday / bank EFT    |

Anything genuinely host-specific (token storage, future file pickers / push) is registered by the
host; host-agnostic services (map, geolocation, API client, auth state) are registered by
`AddNamFixSharedUi()`.

---

## 7. Monetization — transaction commission

Revenue is a **configurable percentage commission per transaction** (not subscriptions).

**Flow** (`TransactionService.CreateAsync`):
1. Resolve the effective rate via `CommissionRepository.ResolveRateAsync` — most specific active rule
   wins: **Provider override > Category override > Platform default** (`CommissionRules` table,
   `CommissionScope` enum). Default platform rate is seeded at 10%.
2. `CommissionAmount = round(Gross × Rate)`, `NetPayout = Gross − Commission`.
3. `IPaymentService.HoldAsync` holds the gross from the client → status **Held** (commission captured).
4. `PayoutAsync` later releases `NetPayout` to the provider → status **PaidOut**.
   This **hold-then-release** pattern guarantees commission is secured before payout.

**Transaction data model** (`Transactions` table / `Transaction` entity): gross amount, commission
rate applied, commission amount, net payout, status (`Pending/Held/PaidOut/Refunded/Failed`),
currency (NAD), payment reference, and `Created/Held/PaidOut` timestamps.

**Reporting:** admins get period revenue totals sliced **by category and by town**
(`AdminController.Revenue` → `TransactionRepository.GetRevenueReportAsync`); providers get a
gross / commission / net / pending earnings rollup (`TransactionsController.Earnings`).

Featured/promoted listings are an optional *secondary* stream and are not the primary model.

---

## 8. Running locally

**Prerequisites:** .NET 10 SDK, SQL Server (LocalDB / Express / full), Grate (`dotnet tool install -g grate`).

```bash
# 1. Apply schema + seed (see NamFix.Api/Migrations/README.md for the full --folders command and caveats)
grate --connectionstring="Server=localhost;Database=NamFix;Trusted_Connection=True;TrustServerCertificate=True;" \
      --sqlfilesdirectory=./NamFix.Api/Migrations --databasetype=sqlserver \
      --folders='{"runAfterCreateDatabase":{"type":"Once"},"up":{"type":"Once"},"views":{"type":"AnyTime"},"sprocs":{"type":"AnyTime"},"permissions":{"type":"EveryTime"},"seed":{"type":"EveryTime"}}'

# 2. Run the backend API
dotnet run --project NamFix.Api      # https://localhost:7111

# 3. In a second terminal, run the front-end
dotnet run --project NamFix.Web      # https://localhost:7213
```

Then browse to the client's HTTPS URL (`https://localhost:7213`). Sample logins (password `Password123!`): `admin@namfix.na`,
`aqua@namfix.na`, `spark@namfix.na`, `auto@namfix.na`.

**Config:** `NamFix.Api/appsettings.json` holds `ConnectionStrings:DefaultConnection`, the `Jwt`
section, and `Cors:Origins`. The connection string is always taken from this file. Other settings
(e.g. the JWT signing key) may be overridden with a `NAMFIX_`-prefixed env var (double-underscore for
nesting), e.g. `NAMFIX_Jwt__SigningKey`.

---

## 9. Bookings (negotiated jobs) + realtime

A **Booking** is the negotiated job between a client and a provider, and the **only** way money moves:
a client can never pay an arbitrary amount — payment is locked to a booking's invoice.

- **Lifecycle** (`BookingStatus`): the proposed time bounces back and forth, with the status encoding
  *whose turn it is to approve* — `PendingProvider` (client proposed, provider must respond) ⇄
  `PendingClient` (provider proposed, client must respond) → `Scheduled` (time agreed) →
  `Completed` (provider marked done + set an invoice amount) → `Paid`. Plus `Cancelled` (either party)
  and `Declined` (provider). The whole state machine + authorization lives in
  `BookingService` (`NamFix.Application`); the client never re-implements the rules.
- **Payment**: `BookingService.PayAsync` is the only client payment path in the UI. It validates the
  booking is `Completed`, then calls `TransactionService.CreateAsync` with the booking's own
  `InvoiceAmount` (commission/hold-then-release as in §7) and links the resulting `TransactionId`.
  The provider profile's old free-form "pay any amount" box was replaced by a **Request a booking**
  form. (`TransactionsController.Create` still exists for admin/testing but is not surfaced.)
- **Chat + location + invoice file**: each booking has a message thread (`BookingMessages`), an
  optional client-shared location (text + coords), and a provider-uploaded invoice file stored in
  the DB (`BookingAttachments`, served via `GET/POST api/bookings/{id}/invoice`).
- **Notifications**: every booking event writes a `Notifications` row for the *other* party and is
  pushed live. The nav shows a 🔔 bell with unread count + dropdown (`NotificationsBell`).
- **Realtime (SignalR)**: `BookingHub` (`/hubs/notifications`, **authenticated** — JWT passed in the
  query string, see `Program.cs` `OnMessageReceived`) puts each connection in a per-user group.
  `IBookingRealtimeNotifier` (Shared.Contracts) decouples the service layer from SignalR;
  `SignalRBookingNotifier` (API) is the implementation. The client `NotificationService` holds the
  hub connection + notification state and raises `BookingChanged` / `MessagePosted` so open booking
  views update live. This is separate from the `StatusHub` heartbeat in §Connectivity.

## 10. Vertical slice implemented

Auth (register/login/refresh, JWT) · Provider CRUD (self-managed profile, towns, tags, map pin,
availability, admin approval) · Full-text + filter + "near me" search · Leaflet map display ·
Reviews (verified flag) · Transaction with commission calculation + earnings/revenue reporting ·
**Bookings: time negotiation, location share, invoice upload, booking-locked payment, in-booking
chat, and live notifications over SignalR** · Dark/light theme (dark default) · WhatsApp deep-link
contact. Payment gateway and MAUI app are abstracted/stubbed for later.
