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

```bash
grate \
  --connectionstring="Server=localhost;Database=NamFix;Trusted_Connection=True;TrustServerCertificate=True;" \
  --sqlfilesdirectory=./db \
  --databasetype=sqlserver \
  --folders='{"runAfterCreateDatabase":{"type":"Once"},"up":{"type":"Once"},"views":{"type":"AnyTime"},"sprocs":{"type":"AnyTime"},"permissions":{"type":"EveryTime"},"seed":{"type":"EveryTime"}}'
```

PowerShell (single line):

```powershell
grate --connectionstring="Server=localhost;Database=NamFix;Trusted_Connection=True;TrustServerCertificate=True;" --sqlfilesdirectory=./db --databasetype=sqlserver --folders='{"runAfterCreateDatabase":{"type":"Once"},"up":{"type":"Once"},"views":{"type":"AnyTime"},"sprocs":{"type":"AnyTime"},"permissions":{"type":"EveryTime"},"seed":{"type":"EveryTime"}}'
```

Grate creates the `NamFix` database if it does not exist, runs `up/` once, then re-applies the
AnyTime/EveryTime folders. It records history in the `grate` schema (`ScriptsRun`, `Version`).

## Run in CI

Use an env var for the connection string and the same command:

```bash
grate --connectionstring="$NAMFIX_CONNECTION_STRING" --sqlfilesdirectory=./db --databasetype=sqlserver \
      --folders='{"up":{"type":"Once"},"views":{"type":"AnyTime"},"sprocs":{"type":"AnyTime"},"permissions":{"type":"EveryTime"},"seed":{"type":"EveryTime"}}' \
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
