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

## Roadmap / TODO

Hoshi Bot currently serves **Alliance Discords** (the full feature set: absences, shield/raid
alerts, Territory Capture, RoE violations, tickets, announcements, diplomacy, anonymous
messages, Setup Wizard). The following is unbuilt — a raw backlog, not a scoped/prioritized
spec, several items depend on each other (e.g. the boarding wizard feeds nickname tagging;
RoE alliance lists feed the diplomacy channel structure).

### Server & Veil Group Discords (new audience, not built yet)

- Diplomacy group — a server-wide diplomacy construct (distinct from the existing per-alliance
  diplomacy), grouping which alliances are allied/enemy/etc. at the whole-server level.
- Diplomacy channel per alliance in that group — split into a "RoE Diplomacy" category
  (per-alliance RoE compliance discussion) and a separate "Non-RoE Diplomacy" category (general
  war declarations, general diplomacy chat). Confirmed pattern from a real reference server.
- RoE alliances — a listing of alliances recognized under the server's Rules of Engagement,
  with an application flow for alliances to join it.
- No-RoE alliances — a separate tracked list of alliances explicitly excluded from the RoE.
- Rogue players — two pieces: a published rogue-policy document (same versioned-document
  pattern as RoE, below) plus a live rogue-listing (an actual tracked roster), plus a "no-id"
  holding channel/role for members who haven't completed boarding yet.
- **RoE governance workflow** (not just a static multilingual doc):
  - Versioned with a change pipeline — proposal → discussion → council vote → publish with a
    future effective date (a real reference server's RoE embed literally has "Last Change" and
    "Validity from" timestamps in its footer).
  - Structured content — numbered rules, an "Exceptions" sub-list per rule, and a Definitions
    glossary section (e.g. Warship, Miner, OPC/UPC, Zero/D-Node, Full Cargo).
  - Per-language **channels**, not a language switcher — separate channels per language, each
    mirroring the same structured embed, kept in sync from one template.
- Boarding wizard — new member picks alliance, server, and in-game player name, driving an
  automatic Discord nickname change to match.
- Role application with human confirmation — a role request is only granted after mod team or
  alliance leadership approves it, not automatically.
- Cross-Discord role sync — if a player's home alliance also runs Hoshi Bot in their own
  Discord, sync that player's role/status between the server Discord and the alliance Discord.

### Governance bodies (new, not previously scoped)

Two distinct cross-alliance bodies, separate from any single alliance's own leadership:

- **Alliances Council** — owns the RoE proposal/discussion/vote pipeline above; has its own
  deliberation ("chamber") and voting channels.
- **Mediation Council** — a separate dispute-resolution body with published guidelines, a
  requests channel (alliances/players file mediation requests), and its own mediator vote
  channel. Don't conflate this with the Alliances Council — different purpose and membership.

### Cross-server events ("Incursions") — new, not previously scoped

Recurring scheduled PvP events against another whole server, with a template-shaped
announcement every time: event name/dates/duration, scoring-rule changes for the event, a
pre-event safety window (shields drop above a system threshold), a "server purge" at a fixed
offset before start (prevents cheesing), a declared ceasefire between the home server's
alliances during the event, the opposing server's ID, and a temporary access-restricted
channel group that only exists for the event window. Good fit for a reusable scheduled-event
templating feature alongside the existing Quartz jobs in `HoshiBot.Discord/Scheduling/`.

### Applies to all server-type Discords (Server + Veil Group)

- Per-channel default language, not just per-guild.
- stfc.pro sync — alliance/player name synchronization against the external stfc.pro data
  source, not just in-guild data.
- Conditional nickname tagging — format as `[server][alliance-tag] Player Name`, conditionally:
  no tags for a player from the Discord's own home alliance/server, but foreign players (from
  another server or an external allied alliance) get both tags to disambiguate.
- Anonymous coordinate/violation reporting — likely reuses the already-built Anonymous Messages
  feature (built for alliances) rather than needing new engineering, just exposed server-wide.

### Enhancements to the existing Alliance Discord feature set

- Allied-alliance channel/group — dedicated channel or category for allied alliances inside an
  alliance's own Discord, driven by the existing diplomacy status data.
- Player-left-alliance leadership warning — when stfc.pro sync detects a player has left the
  alliance, warn alliance leadership and prompt them to reassign/remove that player's Discord
  roles (human-confirmed, not automatic). The server-Discord equivalent should auto-correct the
  player's roles/tags instead of just warning, since it isn't the player's home alliance.

### Engineering requirement learned from a legacy bot failure

A reference server's legacy (YAGPDB-based) self-service "claim your alliance tag" command
broke in production when a moderator role got reordered above another role in the guild's
hierarchy — Discord bots can only manage roles/nicknames positioned below their own top role.
It failed with a cryptic error rather than a clear one. **Hoshi Bot's boarding/tag-claiming
implementation must detect this failure mode explicitly and surface a clear error to mods**,
not fail silently/cryptically.

### Community Discords (new audience, no concrete feature set yet)

Not tied to one alliance, server, or veil group. Existing generic features (announcements,
tickets, anonymous messages) already carry over; no community-specific features are scoped yet.
