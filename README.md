# Hoshi Bot

A Discord bot for Star Trek Fleet Command (STFC) alliances — raid/shield alerts, absences,
Territory Capture reminders, RoE violation reports, tickets, announcements, and diplomacy
tracking — plus a Blazor web admin panel. Rewritten in C#/.NET from an earlier
[YAGPDB](https://yagpdb.xyz/) Go-template implementation (`hoshi-bot-yagpdb/`, kept as
reference alongside this repo).

## Projects

| Project | Purpose |
|---|---|
| `src/HoshiBot.Domain` | Plain POCOs, enums, and pure logic (duration/scheduling parsers) — no EF Core, no Discord references. |
| `src/HoshiBot.Data` | EF Core `DbContext`, entity configurations, migrations, seed data. PostgreSQL in production, SQLite for local dev. |
| `src/HoshiBot.Discord` | All bot behavior: slash-command/button/modal/menu modules, per-feature services, Quartz scheduled jobs. A plain library — no entry point. |
| `src/HoshiBot.Host` | The Worker Service that actually runs the bot: composition root (DI, Quartz schedules, Discord gateway) plus one gateway handler (`GuildSyncHandler`). |
| `src/HoshiBot.Web` | Blazor Web App admin panel — Discord OAuth2 login, per-guild settings, feature toggles, STFC catalog management. |
| `tools/HoshiBot.Migrator` | Standalone console app that applies pending EF Core migrations against the production PostgreSQL database. |
| `tools/HoshiBot.StfcCatalogSync` | Parses STFC static-data HTML exports (regions/servers/systems/territories) into seed data. |
| `tests/HoshiBot.Domain.Tests` | Unit tests for `HoshiBot.Domain`. |

See [CLAUDE.md](CLAUDE.md) for a deeper explanation of why the project is split this way,
and other conventions worth knowing before making changes.

## Local development

Requires .NET 10 SDK.

```bash
dotnet restore
dotnet build
dotnet test
dotnet format          # run before committing
```

Run the bot:

```bash
dotnet run --project src/HoshiBot.Host
```

Run the web admin:

```bash
dotnet run --project src/HoshiBot.Web
```

Local dev defaults to SQLite (`Database:Provider` = `Sqlite` in each project's
`appsettings.Development.json`, zero-setup file DB at `hoshibot.dev.db`) via
`EnsureCreated()` — no migrations needed locally. **After any entity/schema change, delete
`hoshibot.dev.db`** so it gets recreated with the current schema.

### Secrets

Never commit tokens/passwords to `appsettings*.json`. Use user-secrets locally:

```bash
dotnet user-secrets set "Discord:Token" "<bot-token>" --project src/HoshiBot.Host
dotnet user-secrets set "Discord:ClientId" "<oauth-client-id>" --project src/HoshiBot.Web
dotnet user-secrets set "Discord:ClientSecret" "<oauth-client-secret>" --project src/HoshiBot.Web
```

## Production deployment

`compose.yaml` runs three services: `bot` (HoshiBot.Host), `web` (HoshiBot.Web), and
`postgres`. Both app services read secrets from environment variables — see
`compose.yaml` for the full list (`DISCORD_TOKEN`, `POSTGRES_PASSWORD`,
`PUBLIC_WEB_BASE_URL`, etc.).

Migrations are applied via `HoshiBot.Migrator`, not automatically by the bot/web
processes:

```bash
HOSHIBOT_CONNECTIONSTRING="Host=...;Database=hoshibot;Username=...;Password=..." \
  dotnet run --project tools/HoshiBot.Migrator
```

### EF Core migrations

Generate new migrations against `HoshiBot.Data` (uses `HoshiBotDbContextFactory`'s
design-time Npgsql connection):

```bash
cd src/HoshiBot.Data
dotnet ef migrations add <Name>
```

**Always inspect a scaffolded migration before trusting it** — EF's rename detection is
unreliable and will sometimes scaffold a `DropTable`/`CreateTable` (or drop/add column)
for what's actually a rename, silently discarding production data on deploy. Rewrite those
by hand as `RenameTable`/`RenameColumn`/`RenameIndex` (+ `ALTER TABLE ... RENAME
CONSTRAINT ...` for PK/FK names) instead.
