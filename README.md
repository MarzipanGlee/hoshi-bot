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

## License

Copyright (C) 2026 MarzipanGlee. Licensed under the GNU Affero General Public License v3.0
— see [LICENSE](LICENSE) for the full text.

Hoshi Bot is an unofficial, fan-made tool and is not affiliated with, endorsed, or sponsored
by Scopely, CBS Studios Inc., or Paramount Pictures Corp. Game images, icons, and logos used
by the bot and its web admin panel are the property of Scopely and used for identification
purposes only.

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

`compose.yaml` runs four services: `bot` (HoshiBot.Host), `web` (HoshiBot.Web), `migrator`
(HoshiBot.Migrator, profile `migrate` — only runs on demand, not part of `up`), and
`postgres`. Both app services read secrets from environment variables — see `compose.yaml`
for the full list (`DISCORD_TOKEN`, `POSTGRES_PASSWORD`, `PUBLIC_WEB_BASE_URL`, etc.).

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

Hoshi Bot currently serves **Alliance Discords** (the full feature set: absences, shield/raid
alerts, Territory Capture, RoE violations, tickets, announcements, diplomacy, anonymous
messages, Setup Wizard). The following is unbuilt — a raw backlog, not a scoped/prioritized
spec, several items depend on each other (e.g. the boarding wizard feeds nickname tagging;
RoE alliance lists feed the diplomacy channel structure).

### General features (all audiences, not built yet)

- Manage polls — a general-purpose poll creation/management tool usable by any Discord
  (Alliance, Server, Community), not tied to one audience. Could later become the underlying
  mechanism for the Council/RoE/Mediation vote channels below, rather than a separate
  implementation, but stands on its own as a feature regardless.
- Rate-limit visibility — NetCord (the Discord library) already handles rate limiting
  automatically (per-route + global buckets, `Retry-After` aware), but nothing in the codebase
  subscribes to its `RateLimited` event, so throttling happens silently. Subscribe to it and
  surface an active-rate-limit banner in the web admin backend, purely for operator visibility
  — not a correctness fix, since requests already succeed, just delayed.

### Web admin UX (done)

Settings is now one page per feature (`Components/Pages/Manage/Guilds/Features/`) plus a
Global Settings page for what's genuinely guild-wide, each setting with a real description.
The Dashboard lists every feature grouped by audience (Alliance / Server & Veil Group /
Community) with an inline enable switch and a Configure link per card, independently
toggleable per audience for the 5 features that serve more than one. The Setup Wizard asks
which audience(s) a guild serves up front and skips the Scope step for Community-only
guilds; per-feature enabling now happens from the Dashboard instead of a wizard step. Every
feature's settings (channel/role IDs, and TerritoryCapture's instructions text) live in a
generic per-(guild, feature, audience, key) store (`GuildFeatureSettingsService`) instead of
flat `GuildSettings` columns — see that service's doc comment for the shape.

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

### Incursion advance-warning announcement (external API found, not built)

An STFC stats site (`gilli.site`) exposes `/api/events` — one row per known recurring event
type (`incursions`, `alliance_tournaments`, `sarris_invasions`, `flashpoint`) showing its most
recent `event_start`/`event_end`/`active` state. Planned: poll it, and when `incursions`'
`event_start` changes to a new future date not seen before, post a Discord announcement warning
players ahead of time (ties into the "Cross-server events" item above). Two things unresolved
before building: whether this API actually gets updated with advance notice at all (every row
observed so far was a past, inactive event — needs watching over time to confirm), and whether
to post through a new dedicated channel/setting or the existing Announcements pipeline.

### Server up/down + maintenance notification (external API found, not built)

The same stats site also exposes `/api/server-status` — one row per real STFC server (113
total), shaped `{id, name, region: {id, description, num}, status, player_transfer_state:
{transfer_in, transfer_out, authenticated}, priority, maintenance}`. Server 164 confirmed
present (region `eu-west-1`, "Ireland (EU)"). Planned: poll it and post a Discord announcement
when `status`/`maintenance` changes for the tracked server, so players know a maintenance
window is why the game is unreachable instead of assuming something's broken locally. Checked
2026-07-08: every server showed `status:1`/`maintenance:"0"` (all up) — the actual down/
maintenance values are unconfirmed, need a real state change to verify. `player_transfer_state`
looks like a separate server-merge/transfer concern, not needed for basic up/down/maintenance.

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

### Guild removal cleanup (engineering, not previously scoped)

No handler exists yet for the bot losing access to a guild — `GuildSyncHandler`
(`HoshiBot.Host`) only handles the gateway's guild-create event (join/reconnect), with no
symmetric handler for the bot being kicked, banned, leaving, or the guild being deleted
outright. Discord's own gateway event doesn't distinguish "bot removed" from "guild
deleted" — both surface identically; the only thing to filter out is the event's
`unavailable: true` case, which means a temporary Discord-side outage, not real removal.
Every per-guild entity built so far already cascade-deletes from `DiscordGuilds`, so the
DB-level cleanup mechanism already exists — what's missing is the trigger. Main open design
question before building: delete immediately on the event (simple, but loses all config if
the bot is kicked and re-invited by accident within minutes), or a grace-period soft-delete
(mark a `PendingRemovalAt` timestamp, a cleanup job finalizes the delete after N days,
cancelled if the guild-create event fires again first) — safer, but adds a new sweep job
and a cancel-path to get right.

### Community Discords (new audience, no concrete feature set yet)

Not tied to one alliance, server, or veil group. Existing generic features (announcements,
tickets, anonymous messages) already carry over; no community-specific features are scoped yet.
