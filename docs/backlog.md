# Backlog

Deferred ideas / follow-ups, not scheduled yet.

## Encrypt per-guild secrets stored in the DB — DONE

The AI-chat feature stores each guild's Google Gemini **API key** in `GuildFeatureSettingText`
(`AiChatSettingKeys.ApiKey`). It is now **encrypted at rest** (AES-256-GCM): a symmetric key from
config (`Secrets:EncryptionKey`, injected via env/user-secrets like `Discord:Token`) is applied
transparently inside `GuildFeatureSettingsService.GetSecretAsync`/`SetSecretAsync` via
`SettingSecretProtector`, so callers still pass/receive a plain string. Stored form is
`enc:v1:<base64>`; unprefixed values are treated as legacy plaintext and upgraded to ciphertext on
first read once the key is configured. With no key configured (dev), values are stored plaintext —
encryption is a deployment concern (`Secrets__EncryptionKey` in `.env`, wired to the `bot` + `web`
services in `compose.yaml`). The mechanism is generic and reusable for any future per-guild secret.

Possible follow-ups: key **rotation** (re-encrypt all secrets under a new key — today the key must
stay stable) and moving the `Secrets:EncryptionKey` itself into a real secrets manager rather than
an env var.

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
  prompt). The *"stronger model for the actual answer"* half is also done via **complexity routing**
  (`AiChatSettingKeys.RouterModel`): when set, a cheap classifier tags each answer SIMPLE/COMPLEX and
  answers simple ones with the cheap model, escalating only complex ones to the main `Model`. Opt-in
  per guild, provider-agnostic; motivated by Gemini's per-model RPD limits (flash-lite 500/day vs
  flash 20/day). Possible follow-up: fold the router into the gate for passive messages (one 3-way
  NO/SIMPLE/COMPLEX call instead of two).
- **Ollama Cloud / per-guild endpoint** — if a guild ever needs its own remote Ollama, promote the
  base URL (+ optional key) from deployment config to a per-guild setting; the
  `AiChatCompletionRequest.ApiKey` field already exists to carry a token.
- **Model pull automation** — an init/sidecar that pulls the default model on stack up, instead of
  the documented one-time `docker compose exec ollama ollama pull <model>`.
- **Streaming responses** — ✅ **done**: `IAiChatProvider.GenerateStreamAsync` (Ollama `Stream=true`
  / Gemini `GenerateContentStreamAsync`); directly-addressed answers post a placeholder then edit it
  in place as the answer streams (throttled to Discord's edit rate). Passive answers stay post-once
  (they may end in `[NO_ANSWER]` silence). Also fixed alongside: the concurrent-question drop (a 2nd
  question posted mid-answer now queues instead of being dropped — bounded per channel).

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
- **Edit/delete reconcile** — ✅ **done**: `AiChatIndexReconcileHandler` handles MESSAGE_UPDATE
  (re-index, dropping the stale embedding when the text changed) and MESSAGE_DELETE/_BULK (prune the
  rows), so edits to older messages and deletions no longer leave stale content in the index.
- **Structured-data answers** — ✅ **done** (first cut): `AiChatService.BuildTerritoryCaptureFacts`
  injects this week's owned zones (tier + capture window as `<t:…:t>` timestamps) for the guild's
  TC-enabled alliances into the prompt as authoritative "facts", so the bot answers "which zones do
  we hold / when's the next capture?" directly instead of deflecting to the digest. Extendable to
  other structured tables (rosters, server status, events) the same way.
- **Rate limiting / cost controls** — per-user / per-channel, once real usage is observed.
- **FTS GIN index** — still none (per-guild language rules out a single constant config); revisit
  (functional GIN per language, or a stored tsvector repopulated on language change) only if needed.

## AI chat — image/vision support (indexing + live replies)

Hoshi is entirely text-only today, in both places that matter — confirmed no code path sends
image data to a model or extracts anything from one:

- **Indexing**: `AiChatIndexService.RenderMessageText` only reads `message.Content` and
  `message.Embeds`; it never looks at `message.Attachments`. A bare image post with no caption
  text and no embed renders to an empty string and is silently dropped — not even indexed as a
  placeholder. Community-authored reference material that's commonly posted as an infographic
  (e.g. a crew-recommendation chart image) is currently invisible to the knowledge base.
- **Live replies**: `AiChatTurn` (`IAiChatProvider.cs`) is a plain `record struct(AiChatRole
  Role, string Text)` — no image/byte field. `GeminiClient.cs` and `OllamaClient.cs` both build
  every turn as a text-only part; neither ever populates Gemini's `InlineData` image part or
  Ollama's `Images` field, even though both underlying APIs support multimodal input (Gemini
  natively; Ollama for vision-capable models like llava/gemma3/qwen2-vl).

Adding this is architecturally straightforward but a real feature, not a tweak: extend
`AiChatTurn`/`AiChatCompletionRequest` with an image payload, fetch attachment bytes, and wire it
through both the indexing pass (so an image-only post contributes *something* — at minimum a
model-generated caption/description to index as text) and/or live-turn building (so a directly
attached image can be reasoned about in the moment). Deferred until it's clear how much of the
guild's actual reference material is image-only vs. text/embed-based — worth auditing the crew
guide channels first to see how much this would actually move the needle.

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

## Bug: `Absence.CreatedAt` never set (shows `0001-01-01`)

Seen 2026-07 on the `Manage/Database/Absences` debug page: every `Absence` row shows
**`CreatedAt = 01/01/0001 00:00:00 +00:00`** — the default `DateTimeOffset`, i.e. the field is
never assigned when an absence is created via the Discord flow
(`AbsenceModalModule` / the absence-creation service in `HoshiBot.Discord`). Likely the `Absence`
entity is inserted without `CreatedAt = DateTimeOffset.UtcNow`, so EF persists the CLR default.
Fix server-side at creation (the Database page is read-only debug output): set `CreatedAt` where
`Absence` rows are inserted, and check whether the edit flow (`EditsAbsenceId`) needs it too.

## STFC in-game languages — i18n reference

STFC's in-game Language Settings offer **9 languages** (per a 2026-07 screenshot of the game's
Language Settings screen): English, Français, Italian, German, Spanish, Russian, Portuguese,
Japanese, Korean. Keep this set in mind for any future feature that mirrors the player's game
language — e.g. per-language rules channels (the bot already has Rules DE/EN channels on
`GuildAlliance`), localized notifications, or AI-chat / announcement translation targets. The
bot's Discord-facing text is currently German-primary; the Web admin UI is English-only.

## Stfc CRUD scaffold — remaining DRY (deferred: risk > reward)

A 2026-07 housekeeping round DRY'd most of the machine-scaffolded `Manage/Stfc/**` CRUD pages
(`DbContextPageBase` for the 35 list pages, `FormField` for the Create/Edit fields,
`DeleteConfirmation` for the 12 Delete pages, `ImportForm` for the simple Import pages). Three
pieces were **left as-is on purpose** — the duplication is real but the extraction is either
risky or fragile for what are low-traffic admin pages:

- **Create/Edit form-markup shell (`CrudFormShell`).** After `FormField`, the only shared markup
  left on each Create/Edit page is the thin outer wrapper (`EditForm` + `DataAnnotationsValidator`
  + `ValidationSummary` + row/col + Save button + Back-to-List) — ~10 lines/page across ~23 pages.
  **Risk:** wrapping that `EditForm` in a component moves the real inputs (with `@bind-Value`,
  `[SupplyParameterFromForm]` model binding, and validation) *across a component boundary in static
  SSR*. `DeleteConfirmation` proved the pattern works for a form with **no** inputs, but a Create/Edit
  form adds model binding + validation that a `dotnet build` can't verify — only creating/editing a
  row confirms it. Low reward, highest risk of the effort → skipped. If picked up: build the shell,
  then live-verify a create **and** an edit on every distinct entity, and be ready to revert.

- **Edit concurrency-save code-behind.** The 12 `Edit.razor` pages each repeat ~20 lines of
  `context.Attach(entity).State = Modified; try SaveChangesAsync catch DbUpdateConcurrencyException →
  EntityExists? NotFound : throw; NavigateTo(list)`. **Risk:** a generic base (`StfcEditPageBase<T>`)
  needs each page to supply a `DbSet<T>` accessor **and** an `Expression<Func<T,bool>>` key predicate
  (a C# `KeyMatches` method can't be EF-translated), plus `[SupplyParameterFromForm]`/`FromQuery` on
  base-class properties — more surface area and EF-translation pitfalls for a modest net saving.
  Only worth it as part of doing the form shell above.

- **PlayerPages / ServerPages imports.** These two Import pages did **not** fit the extracted
  `ImportForm<TResult>`: PlayerPages has region/server `<Select>` dropdowns + multi-file upload +
  a "pick a server first" guard; ServerPages stages **two** separate file uploads (servers + invites)
  behind an explicit "Run Import" button. Bespoke enough that a shared component would need to model
  those flows — left as their own pages.

## Bug: Territory Capture reminders + weekly digest — ✅ done (2026-07-24)

The screenshot "Gebietsübernahme {Zone} **in 15 minutes**" turned out to be the **legacy YAGPDB
bot** — the new bot had **no** per-capture reminder at all, and none of its TC messages were ever
removed. Ported/fixed the whole lifecycle:

- **Per-capture reminder**: new `TerritoryCaptureReminderJob` (5-min) posts one ~30-min-before
  "capture soon" ping per zone (relative `<t:…:R>` time + "Abmelden" unsubscribe button), matching
  legacy's single reminder (not a 30+15 cascade).
- **Cleanup**: a unified `TerritoryCaptureSentMessage` table (Kind + ExpiresAt + dedup key) tracks
  every TC message, and the reminder job's sweep deletes each on the legacy schedule — **Single at
  capture End**, **Daily +1d**, **Weekly +7d** (deleting the weekly also drops its pin). The unique
  `(GuildAllianceId, DedupKey)` index makes every post idempotent.
- **Week window**: TC week anchor changed **Wednesday → Tuesday** (Scopely's current cadence, no
  capture-free day anymore); the weekly digest now posts **Monday** previewing the **upcoming**
  Tue→Mon week (`GetUpcomingWeekStart`), and the daily digest bases its "tomorrow" on next week so
  the new week's opening day isn't skipped.

## Territory Capture "Services" (Dienste) reminder for officers — ✅ infra done (2026-07-25)

The legacy YAGPDB bot had a fourth TC reminder type — **"Services" / "Dienste aktivieren"** — a
post-capture reminder for officers fired ~**5 min after each capture ends**, posted to a **separate
services channel** (`$remindersServicesChannel`). The **infrastructure is now ported** (the
2026-07-24 pass had done Single + Daily + Weekly only): a `Services` `TerritoryCaptureMessageKind`,
a second pass in `SendCaptureRemindersAsync` firing in the ~5-min window after `slot.End` (dedup
`services-…`, swept +6h), a `SendServicesReminderAsync` posting a branded "Dienste aktivieren für
{Zone}" embed that pings a configurable **services role**. The channel setting was the existing
`GuildAlliance.RemindersServicesChannelId`, **migrated into the TerritoryCapture feature settings**
(`ServicesChannel`, like `DigestChannel` before it — column dropped, picker moved into the TC
editor); a new **`ServicesRole`** feature setting was added (a dedicated role, not the RankRoles
Commodore role).

**Per-zone service list — ✅ done (2026-07-25)** via the territory.lol synchronizer (below): the
Services reminder now lists each zone's actual services (ordered, English game-term names) for the
alliance's server, falling back to the generic nudge when a zone/server has no synced services. The
earlier "blocked on data" concern is resolved — the real territory→service mapping isn't the static
`service_list_ids` (an unresolvable id space) but the per-server `service_slots_{server}.json`, and
service names/rarity come from `territory_service_specs` + `translation.json` (`services_name_{loca}`).

Possible follow-ups: (a) **German translation** of service names (currently English game terms; the
framing text is German); (b) legacy's **mandatory/optional split** — the game data has no such
distinction, so all of a zone's services are listed in order (a curated split would be a manual
overlay, not in the source data).

## Territory Capture service sync — follow-ups

The territory.lol synchronizer (`TerritoryServiceSyncService`, manual "Sync now" button on
`/manage/stfc/territory-services`) is `meta.json`-gated (skips when `tcSeason`/`generatedAt` are
unchanged) and fetches `service_slots` only for servers with a linked alliance. Deferred:

- **Scheduled auto-sync** — a Quartz job (meta.json-gated) so the catalog/mapping refresh each TC
  season without a manual click. Model on the existing external-fetch jobs in
  `HoshiBot.Discord/Scheduling/`.
- **Richer territory-metadata sync** — the same `static_*.json` also carries per-region takeover
  windows (duration/start_hour/weekday), tier, neighbours, node/system links — richer than the
  current hardcoded `StfcTerritorySeedData` (single global weekday/time, tier-derived duration).
  Ingesting it would let the TC scheduler move off the single-weekday model to real per-region
  windows — a larger change touching `TerritoryCaptureScheduler`.

## Territory Capture "Services role" sync

The TC Services reminder pings a dedicated `ServicesRole` (TerritoryCapture feature setting). Today
an admin sets/creates that role manually. Follow-up: a sync feature/job that keeps that role
assigned to exactly the members who hold the **Commodore rank role** (RankRoles feature,
`RankRolesSettingKeys.CommodoreRole`), so the officer ping stays in sync without manual role
management. Model on `TerritoryCaptureRoleSyncJob`/`RankRoleSyncJob`.
