# Backlog

Deferred ideas / follow-ups, not scheduled yet.

## Encrypt per-guild secrets stored in the DB

The AI-chat feature (`GuildFeature.AiChat`) stores each guild's Google Gemini **API key** as
**plaintext** in `GuildFeatureSettingText` (`AiChatSettingKeys.ApiKey`), configured in the Web
admin panel. This was a deliberate "start simple" choice — the DB now holds a live third-party
secret in the clear.

Future direction: set a symmetric **encryption key in the bot's config JSON**
(`appsettings`/`IConfiguration`, e.g. `Secrets:EncryptionKey`, injected via env/user-secrets
like `Discord:Token`) and encrypt/decrypt secret-typed settings at rest — ideally transparently
inside `GuildFeatureSettingsService` (or a thin wrapper) so callers still read/write a plain
string. Applies to any future per-guild secret, not just the AI-chat key. Until then, treat DB
dumps/backups as containing live API keys.

## AI chat — Ollama provider: combine models & remote endpoints

Ollama shipped as a **second, per-guild-selectable chat provider** (`IAiChatProvider` +
`OllamaClient`, alongside `GeminiClient`; a guild picks via `AiChatSettingKeys.Provider`, reached
via the shared `ollama` compose service at `Ollama:BaseUrl`). Deferred extensions:

- **Combine models for best results** — (a) an Ollama **embedding model** driving **pgvector
  semantic retrieval** — ✅ **done** (see "semantic retrieval" below: `embeddinggemma` +
  hybrid FTS/vector RRF in `AiChatIndexService.SearchAsync`); (b) a small fast **gate** model
  deciding answerable for passive listening + a stronger model for the actual answer — ✅ **done**:
  `AiChatService` runs a one-word YES/NO gate (`AiChatSettingKeys.GateModel` / provider
  `DefaultGateModel`) before the expensive retrieval + main generation on non-addressed messages.
  Strictly additive — it only suppresses on a confident NO; YES/ambiguous/failure fall through to
  the main model (+ its `[NO_ANSWER]`). Gemini defaults on (a flash-lite); Ollama is opt-in via
  `Ollama:GateModel` (the small model must be pulled). Possible follow-up: a `MaxOutputTokens` cap
  on `AiChatCompletionRequest` to hard-bound the gate response (today it relies on the one-word
  prompt).
- **Ollama Cloud / per-guild endpoint** — if a guild ever needs its own remote Ollama, promote the
  base URL (+ optional key) from deployment config to a per-guild setting; the
  `AiChatCompletionRequest.ApiKey` field already exists to carry a token.
- **Model pull automation** — an init/sidecar that pulls the default model on stack up, instead of
  the documented one-time `docker compose exec ollama ollama pull <model>`.
- **Streaming responses** — Ollama `/api/chat` streaming for a live "typing" edit, if the current
  post-once reply feels slow with local models.

## AI chat — retrieval quality & memory

Retrieval now grounds answers with a **hybrid** search over the persistent content index
(`AiChatIndexedMessage` + `AiChatIndexService`): per-guild-language **full-text search** fused
(Reciprocal Rank Fusion) with **pgvector semantic search** over local Ollama `embeddinggemma`
embeddings, so both exact terms and paraphrases match. The backfill job indexes **entire channel
history progressively** (paging backward in bounded per-run steps, tracked per channel in
`AiChatBackfillState`) and fills embeddings in a capped per-run pass. Still deferred:

- **ANN vector index** (HNSW/IVFFlat) — v1 does a sequential cosine scan (fine at per-guild
  knowledge scale, mirrors the no-GIN FTS decision); add an index if a guild's index grows large.
- **Alpine-based pgvector image** — a custom `Dockerfile` from `postgres:16-alpine` + pgvector to
  avoid the glibc/musl libc switch (and the one-time `REINDEX DATABASE` it requires) that came with
  moving to `pgvector/pgvector:pg16`.
- **Larger / different embedding model** (e.g. `bge-m3`, 1024d) — needs a `vector(N)` dimension
  migration + full re-embed (the pass already self-heals a *same-dimension* model swap via the
  `EmbeddingModel` column).
- **Persistent conversation memory** — a conversation-history table if the short recent-history
  window proves too small for good multi-turn memory (today's memory is the live recent-message
  fetch of the current channel).
- **Edit/delete reconcile** — prune index rows for deleted messages; catch edits beyond the
  backfill's recent-message re-index window.
- **Rate limiting / cost controls** — per-user / per-channel, once real usage is observed.
- **FTS GIN index** — still none (per-guild language rules out a single constant config); revisit
  (functional GIN per language, or a stored tsvector repopulated on language change) only if needed.

## Multi-alliance: per-alliance contact buttons & announcements

The multi-alliance work made feature settings/toggles and most Discord behaviour per-alliance
(Territory Capture, Diplomacy, RoE, Absences, Alerts opt-in, Rank/Ops roles). Two flows still
resolve to the guild's **primary** linked alliance — fully correct for single-alliance guilds
(every current guild), but not yet true per-alliance for coalition guilds:

- **Command Bridge contact buttons** (`CommandBridgeAdminModule.GetConfiguredContactAudiencesAsync`,
  `CommandBridgeButtonModule`, `TicketService`, `AnonymousMessageService`): a coalition guild
  should show one "Führungsstab kontaktieren" button per configured alliance (labelled with the
  tag), and open the ticket/anon message against that alliance's channel. Needs the custom-id to
  carry the alliance id (`contact-command-staff:{audience}:{guildAllianceId}` etc.) plus
  **defensive parsing of the old 2-part custom-id** on already-posted hub messages (missing
  segment → primary alliance; admins re-run `/post-command-bridge`).
- **Announcements** (`AnnouncementButtonModule`, `AnnouncementMessageCommandModule`,
  `AnnouncementService.PublishAsync`, `Announcement` entity): publishing to the Alliance audience
  currently targets the primary alliance's channel. To target a specific alliance, thread the
  alliance id through the publish custom-ids and persist `Announcement.GuildAllianceId`; the
  draft-channel reverse lookup already returns `(audience, allianceId)` scopes
  (`FindScopesByValueAsync`) — a coalition guild sharing one draft channel needs a disambiguation
  step (or first-scope-wins, logged).

Both are low-urgency (no coalition guild exists yet) and the risky part is custom-id
compatibility on persistent messages — hence deferred.

## Alliance emblems (icons) — DONE

Implemented. `StfcAlliance.Emblem` (`int?`) stores a 0-based index into
`HoshiBot.Web/wwwroot/images/emblems/emblem_{n:D3}.png` (0–26); rendered via the reusable
`AllianceEmblem` component on the overview cards, top-bar selector, sidebar Alliance group,
and the STFC → Alliances grid, with a visual thumbnail picker on the alliance Create/Edit
pages. The seed (`StfcAllianceSeedData.json`) was refreshed from `data/alliances/alliances`
to carry `emblem` for all 10,045 alliances.

**Mapping confirmed:** the external `emblem` integer is a direct 0-based index into our image
set — across the full 10,045-alliance dump every value falls in exactly 0–26, matching the 27
bundled images 1:1. So a future permitted live sync can persist `emblem` straight into
`StfcAlliance.Emblem` with no translation.

Remaining follow-up: there is no live sync yet (stfc.pro `/api/` polling is still
robots.txt-disallowed — see `docs/stfc-api-requirements.md`), so emblems only refresh via a new
manual snapshot or admin edits.

## Roadmap / TODO (unbuilt features)

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

### Web admin UX — audit plain-HTML pickers against BootstrapBlazor (engineering, not previously scoped)

Every dropdown/input across `Components/Pages/Manage/**` and `Components/Shared/*Picker.razor`
is plain HTML (`<select class="form-select">`, `<InputSelect>`) — BootstrapBlazor is an
installed dependency (its bundle already loaded in `App.razor`) that went unused until the
Guild Audience page's cascading region/server/alliance pickers became its first real
consumer (`<Select>` with `ShowSearch`, `<Collapse>` for the per-audience accordion). Review
the existing pickers (`RolePicker`, `ChannelPicker`, the Stfc catalog CRUD forms' selects,
etc.) for replacement with BootstrapBlazor equivalents for consistency — not urgent, no
functional gap, a quality-of-life pass for later.

Once every guild page (`Components/Pages/Manage/Guilds/**`) has been through this pass,
also revisit the Guild Overview page (`Guilds/Index.razor`, route `/manage/guilds/{id}`) —
it wasn't in scope for the Audience/Scope rework and hasn't been checked against the
BootstrapBlazor-first convention or against whatever UI patterns fall out of the other
pages' passes.

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

### Configurable Territory Capture digest times (per guild, maybe per alliance)

The weekly/daily Territory Capture digest fire times are currently hard-coded in `Program.cs`
(`0 0 9 ? * MON` and `0 0 19 * * ?`, pinned to `Europe/Zurich`). They should be
guild-configurable — and possibly per-alliance, since a guild can run several alliance links
(each already has its own `DigestChannel`/`Instructions`/zone-slot settings via
`GuildFeatureSettingSnowflake`/`Text`). Design notes: store the time (and probably an IANA
time-zone id) as a per-`(guild, alliance)` feature setting; the Quartz cron triggers are global
and code-defined, so per-guild times can't be plain static cron triggers.

Chosen approach: a single sweep job on a cron aligned to the half hour — every 30 min starting
from the full hour, i.e. `0 0,30 * * * ?` (fires at :00 and :30). Each run resolves "now" to each
alliance's configured time zone and sends the digest for every `(guild, alliance)` whose
configured digest time matches the current half-hour slot. Configurable times are therefore
constrained to :00/:30 granularity (fine for this feature) and there's no per-guild trigger
lifecycle to manage — the daily and weekly digests become two such sweeps (or one job that checks
both). Keep the same misfire-replay + persistent-store behaviour the hard-coded triggers now have.
Also expose the time (+ zone) in the Web feature-settings UI. Until then the hard-coded 09:00/19:00
Europe/Zurich is the default for everyone.
