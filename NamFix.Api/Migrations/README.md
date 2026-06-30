# NamFix Database (Grate migrations)

Schema and seed data are managed with [Grate](https://github.com/erikbra/grate). SQL is hand-written
and consumed by the Dapper repositories in `NamFix.Application`.

## Folder layout & run order

| Folder                   | Grate run type | Purpose                                                        |
|--------------------------|----------------|----------------------------------------------------------------|
| `runAfterCreateDatabase/`| Once (on create) | One-time database-level setup after Grate creates the DB.     |
| `up/`                    | **Once**       | Versioned schema: tables, indexes, FKs, full-text catalog.     |
| `views/`                 | AnyTime        | `CREATE OR ALTER VIEW` — re-run whenever the file changes.     |
| `sprocs/`                | AnyTime        | `CREATE OR ALTER PROCEDURE` — re-run whenever changed.         |
| `permissions/`           | EveryTime      | Grants — applied on every run (guarded, idempotent).          |
| `seed/`                  | EveryTime      | Reference + sample data. All scripts are idempotent (MERGE).   |
| `markup/`                | EveryTime (opt-in) | Mock/test data (extra users, providers, reviews, transactions). **Not** part of the normal deploy — loaded separately via `NamFix.Api/deploymarkup.ps1`. Idempotent (MERGE). |

`up/` scripts are sequentially numbered (`0001_…`, `0002_…`) and run **once** in filename order.
Views and sprocs use `CREATE OR ALTER` so Grate can safely re-run them every time they change.

## Install Grate

```bash
dotnet tool install -g grate
```

## Run migrations locally

> ⚠️ The full-text catalog/index in `up/0006_fulltext_providers.sql` **cannot run inside a
> transaction**. Grate runs without a wrapping transaction by default, so do **not** pass
> `--transaction` / `-t`.

> ℹ️ `--folders` uses Grate's **short form** (`folderName=runType`, `;`-separated). The JSON form
> is **not** accepted by the Grate version used here — it parses `--folders` with a `;`/`=`/`:`
> syntax instead. Run types: `Once`, `AnyTime`, `EveryTime`.

> 💡 Easiest path on Windows: just run `NamFix.Api\deploydb.ps1` (schema + seed) and, for mock
> test data, `NamFix.Api\deploymarkup.ps1`. The commands below are the underlying Grate calls.

```bash
grate \
  --connectionstring="Server=localhost;Database=NamFix;Trusted_Connection=True;TrustServerCertificate=True;" \
  --sqlfilesdirectory=./NamFix.Api/Migrations \
  --databasetype=sqlserver \
  --folders="runAfterCreateDatabase=Once;up=Once;views=AnyTime;sprocs=AnyTime;permissions=EveryTime;seed=EveryTime"
```

PowerShell (single line):

```powershell
grate --connectionstring="Server=localhost;Database=NamFix;Trusted_Connection=True;TrustServerCertificate=True;" --sqlfilesdirectory=./NamFix.Api/Migrations --databasetype=sqlserver --folders="runAfterCreateDatabase=Once;up=Once;views=AnyTime;sprocs=AnyTime;permissions=EveryTime;seed=EveryTime"
```

Run interactively, Grate pauses for a "press Enter" confirmation — just press Enter. For unattended
runs (see CI below) add `--silent` to skip it, otherwise Grate hangs waiting for input.

Grate creates the `NamFix` database if it does not exist, runs `up/` once, then re-applies the
AnyTime/EveryTime folders. It records history in the `grate` schema (`ScriptsRun`, `Version`).

## Run in CI

Use an env var for the connection string. `--silent` is **required** here — without a TTY, Grate's
"press Enter" confirmation would hang the build.

```bash
grate --connectionstring="$NAMFIX_CONNECTION_STRING" --sqlfilesdirectory=./NamFix.Api/Migrations --databasetype=sqlserver \
      --folders="up=Once;views=AnyTime;sprocs=AnyTime;permissions=EveryTime;seed=EveryTime" \
      --silent
```

## Sample accounts (from `seed/0005_sample_users_and_providers.sql`)

All use password **`Password123!`**:

| Email             | Role            |
|-------------------|-----------------|
| `admin@namfix.na` | Admin           |
| `aqua@namfix.na`  | Service Provider (AquaFix Plumbing, Windhoek) |
| `spark@namfix.na` | Service Provider (Coastal Sparks Electrical, Swakopmund) |
| `auto@namfix.na`  | Service Provider (Desert Auto Care, Oshakati) |

## Mock test data (`markup/`, optional)

`NamFix.Api\deploymarkup.ps1` loads a larger test set from `Migrations/markup/`: 6 clients, 10 extra
providers (across categories/towns and Active/Pending/Suspended statuses), reviews, and transactions
for revenue dashboards. All `markup` accounts also use password **`Password123!`** and live in a
dedicated GUID range so they never collide with the seed data above. The scripts are idempotent —
re-running refreshes without duplicating. Load it **after** the schema/seed deploy.
