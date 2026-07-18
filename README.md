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
| `src/HoshiBot.Data` | EF Core `DbContext`, entity configurations, migrations, seed data. PostgreSQL in both production and local dev. |
| `src/HoshiBot.Discord` | All bot behavior: slash-command/button/modal/menu modules, per-feature services, Quartz scheduled jobs. A plain library — no entry point. |
| `src/HoshiBot.Host` | The Worker Service that actually runs the bot: composition root (DI, Quartz schedules, Discord gateway) plus one gateway handler (`GuildSyncHandler`). |
| `src/HoshiBot.Web` | Blazor Web App admin panel — Discord OAuth2 login, per-guild settings, feature toggles, STFC catalog management. |
| `tools/HoshiBot.Migrator` | Standalone console app that applies pending EF Core migrations against the production PostgreSQL database. |
| `tools/HoshiBot.StfcCatalogSync` | Parses STFC static-data HTML exports (regions/servers/systems/territories) into seed data. |
| `tests/HoshiBot.Domain.Tests` | Unit tests for `HoshiBot.Domain`. |

See [CLAUDE.md](CLAUDE.md) for a deeper explanation of why the project is split this way,
and other conventions worth knowing before making changes.

## License

Copyright (C) 2026 MarzipanGlee. Licensed under the GNU Affero General Public License v3.0
— see [LICENSE](LICENSE) for the full text.

Hoshi Bot is an unofficial, fan-made tool and is not affiliated with, endorsed, or sponsored
by Scopely Inc., CBS Studios Inc., or Paramount Pictures Corp. Game images, icons, and logos
used by the bot and its web admin panel are the property of Scopely Inc. and used for
identification purposes only.

## Local development

Requires the .NET 10 SDK and Docker (for the local PostgreSQL database).

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

Local dev runs against **PostgreSQL** — the same engine as production — via the `postgres`
service in `compose.yaml`, published on `127.0.0.1:5432` so host-run `dotnet run` can reach
it. Each project's `appsettings.Development.json` sets `Database:Provider` = `Postgres`.
One-time setup:

```bash
docker compose up -d postgres        # start the local database
HOSHIBOT_CONNECTIONSTRING="Host=localhost;Port=5432;Database=hoshibot;Username=hoshibot;Password=hoshibot" \
  dotnet run --project tools/HoshiBot.Migrator   # create/upgrade the schema
```

`docker compose` reads `POSTGRES_PASSWORD` from a gitignored `.env`; the throwaway local
value is `hoshibot`, matching both `appsettings.Development.json` and the design-time
factory default. **After adding a migration, re-run the migrator** to apply it locally.

### Secrets

Never commit tokens/passwords to `appsettings*.json`. Use user-secrets locally. `Host` and
`Web` have separate secret stores, so the bot token must be set on **both** — `Web` needs it
for the `RestClient` it uses to read guild/role/channel data for the admin panel, and
`ClientId`/`ClientSecret` on top for Discord OAuth login:

```bash
dotnet user-secrets set "Discord:Token" "<bot-token>" --project src/HoshiBot.Host

dotnet user-secrets set "Discord:Token" "<bot-token>" --project src/HoshiBot.Web
dotnet user-secrets set "Discord:ClientId" "<oauth-client-id>" --project src/HoshiBot.Web
dotnet user-secrets set "Discord:ClientSecret" "<oauth-client-secret>" --project src/HoshiBot.Web
```

## Production deployment

`compose.yaml` runs five services: `bot` (HoshiBot.Host), `web` (HoshiBot.Web), `migrator`
(HoshiBot.Migrator, profile `migrate` — only runs on demand, not part of `up`), `postgres`
(the `pgvector/pgvector:pg16` image — Postgres 16 with the `vector` extension the AI-chat
semantic index needs), and `ollama` (local LLM backend for the AI-chat feature's Ollama
provider and its embeddings). Both app services read secrets from environment variables — see
`compose.yaml` for the full list (`DISCORD_TOKEN`, `POSTGRES_PASSWORD`, `PUBLIC_WEB_BASE_URL`, etc.).

The AI-chat feature answers per guild via **Google Gemini** (guild-supplied API key) **or**
**Ollama** (the shared local `ollama` service, no key). Ollama does not auto-pull models on
first use, so after the stack is up pull the models once — the chat default plus the embedding
model used for semantic knowledge search (and any model a guild overrides to):

```bash
docker compose exec ollama ollama pull llama3.1:8b     # chat (Ollama provider)
docker compose exec ollama ollama pull embeddinggemma  # semantic search embeddings (all guilds)
```

Chat/embedding models are set via `Ollama__DefaultModel` / `Ollama__EmbeddingModel` (env, `bot`
service); base URL via `Ollama__BaseUrl`. Blanking `Ollama__EmbeddingModel` disables semantic
search (knowledge retrieval falls back to keyword full-text search only). For GPU acceleration,
uncomment the `deploy` block on the `ollama` service.

**Postgres image note:** the `postgres` service uses `pgvector/pgvector:pg16` (Debian-based)
rather than the previous `postgres:16-alpine`. On-disk data is compatible (same PG 16), but the
libc changes (musl → glibc), which can invalidate text-index collation ordering — after the
first switch on an existing volume, run a one-time reindex:

```bash
docker compose exec postgres psql -U hoshibot -d hoshibot -c 'REINDEX DATABASE hoshibot;'
```

`bot`/`web` logs are bind-mounted to `./logs/bot`/`./logs/web` on the host (rolling daily
files, 14 days retained) — see [DEBUG.md](DEBUG.md) for how to pull them for debugging.

### Redeploying

From the repo checkout on the host, run `./deploy.sh` — or the equivalent steps by hand:

```bash
git pull
docker compose --profile migrate build
docker compose --profile migrate run --rm migrator
docker compose up -d
```

Order matters — `build` picks up the new code for all three app images, `migrator` applies
any pending EF Core migrations against Postgres, and only then does `up -d` recreate
`bot`/`web` so they start against the schema the new code expects, not the old one.

**`--profile migrate` is required on the `build` step too**, not just `run` — `migrator`
is gated behind that profile (see above), and plain `docker compose build` silently skips
any service not in the active profile set instead of erroring, leaving it on a stale image.
`db.Database.MigrateAsync()` then reports "Schema is up to date" against whatever
migrations *that* stale build knows about, which looks identical to a successful deploy
right up until `bot`/`web` crash on a column/table the real schema never got.

Migrations are applied via `HoshiBot.Migrator`, not automatically by the bot/web
processes. The `docker compose` invocation above is the normal path; it can also be run
directly (e.g. against prod from a dev machine, without a full redeploy):

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

## Roadmap / TODO

The unbuilt-feature backlog (planned features, engineering follow-ups, and deferred ideas)
lives in [docs/backlog.md](docs/backlog.md).
