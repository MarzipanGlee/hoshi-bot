# Backlog

Deferred ideas / follow-ups, not scheduled yet.

## Branded-embed conversion — remaining messages (deferred subset)

A 2026-07-25 pass converted every user-facing **confirmation/result** message, the **cancel/discard**
replies, and the **feature-disabled guards** to the branded embed template (via the new shared helpers
`EmbedBranding.EphemeralAsync`/`BrandedEditAsync` + `Interaction.SendDelayedEmbedAsync`). A few were left
plain on purpose — decide per-case whether they're worth converting:

- **Member Onboarding DM replies** (`MemberOnboardingModalModule.cs:18`, `MemberOnboardingButtonModule.cs:17`)
  — these run in a **DM** (no `Context.Guild`), so branding needs the review's `GuildId` threaded out of
  `MemberOnboardingService.ConfirmAsync`/`ResolveByNameAsync` (they only return a string today). Small
  service change; deferred so the sweep stayed mechanical.
- **Modal input-validation errors** kept plain: `StfcNewsModalModule.cs:20` ("Could not read that date"),
  `CommandBridgeStaffBetaModule.cs:17` (no beta role configured), `AbsenceModalModule.cs` `ErrorEdit`.
- **Wizard/draft-state text** kept plain: `CommandBridgeButtonModule` "Entwurf nicht gefunden" / "Unbekannter
  Entwurfstyp" (draft expired), `AnnouncementButtonModule.cs:124` ("Vorschau …" severity prompt).
- The `⏳ Processing...` ack placeholder stays plain (transient, replaced on edit).

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

### Configurable Territory Capture digest times (per alliance, DST-aware) — ✅ done (2026-07-25)

The weekly/daily digest fire times are now per-alliance configurable, DST-aware. A single
half-hourly sweep (`TerritoryCaptureDigestSweepJob`, cron `0 0,30 * * * ?`) replaced the two
hard-coded `Europe/Zurich` crons: `TerritoryCaptureDigestService.RunDigestSweepAsync` converts
`UtcNow` into each alliance's timezone and fires its weekly/daily digest when its configured local
time is due (`TerritoryCaptureScheduler.IsWeeklyDigestDue`/`IsDailyDigestDue` — due for every tick
at/after the time on the right local day, with the existing per-day/week dedup making it fire once
and giving automatic catch-up). Split of storage:

- **Timezone** is an alliance-level property (`GuildAlliance.TimeZoneId`, IANA, default
  `Europe/Zurich`), edited on the **Alliance Settings** page — reusable by future schedule-driven
  features, not TC-specific.
- **Weekly/daily times** are TC feature settings (`TerritoryCaptureSettingKeys.WeeklyDigestTime`/
  `DailyDigestTime`, local `HH:mm`, :00/:30 granularity), edited on the TC editor's "Digest
  Schedule" card. Defaults 09:00/19:00 → with the default Europe/Zurich zone this reproduces the
  old cron exactly (DST-stable), so nothing shifts for an alliance that configures nothing.

The two old `TerritoryCaptureWeeklyDigestJob`/`DailyDigestJob` classes are kept (marked
`[Obsolete]`) only so a startup `scheduler.DeleteJob` can remove their persisted triggers without
a missing-JobClass error; delete them once every environment's Quartz store is confirmed clean.
The digest weekday stays fixed (weekly = `TerritoryCaptureScheduler.WeeklyDigestWeekday`, i.e. the
day before the week anchor; daily every day) — only the time-of-day + timezone are configurable.

## Bug: `Absence.CreatedAt` never set (shows `0001-01-01`) — ✅ fixed (2026-07-25)

The `0001-01-01` rows came from the one insert path that skipped `CreatedAt`: the Territory Capture
"Abmelden" button (`TerritoryCaptureButtonModule`), which creates an `Absence` directly instead of
via `AbsenceService`. Set `CreatedAt = DateTimeOffset.UtcNow` there; every other path
(`AbsenceService.InsertAsync`/`CreateEditDraftAsync`) already set it, and the edit flow keeps the
original row's `CreatedAt` (correct). Existing bad rows are transient TC unsubscribe absences that
expire/sweep, left as-is.

## Bug: Absences report reposted instead of edited — ✅ fixed (2026-07-25)

`AbsenceService.PostOrEditAsync` caught **every** `RestException` on the edit path and fell through
to re-posting, so a transient rate-limit (429) or Discord 5xx while editing the persistent
Abwesenheiten report was misread as "message gone" → a duplicate post that orphaned the still-present
message. Fixed to only re-post on a genuine 404, keep the id and retry on any other error (and notify
admins on 403). Possible follow-up (deferred — not seen often enough to warrant it): **auto-clean
orphans** — when a repost *does* happen (real 404), there's no old message to remove, but if
duplicate reports recur we could track and delete the previous message id defensively rather than
relying on manual cleanup.

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
  the new week's opening day isn't skipped. *(Superseded 2026-07-31: the anchor is now **Friday**,
  so the weekly digest posts **Thursday** previewing Fri→Thu. Only the constant changed —
  everything else still derives from it.)*

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

**Mandatory/optional split — ✅ done (2026-07-25)**: a per-(alliance, zone) **Service Selection**
(`TerritoryServiceSelection`, TC feature ExtraPage `/features/territory-capture/service-selection`)
lets leadership curate a zone's services into **must-have** (obligatorisch) / **nice-to-have**
(optional) buckets; the reminder renders the two grouped, numbered lists (canonical slot order),
falling back to the full list for uncurated zones. The game data has no inherent split — this is an
admin-curated overlay.

Possible follow-up: **German translation** of service names (currently English game terms; the
framing text is German).

## Territory Capture service sync — follow-ups

The territory.lol synchronizer (`TerritoryServiceSyncService`, manual "Sync now" button on
`/manage/stfc/territory-services`) is `meta.json`-gated (skips when `tcSeason`/`generatedAt` are
unchanged) and fetches `service_slots` only for servers with a linked alliance.

- **Scheduled auto-sync — ✅ done (2026-07-25)**: `TerritoryServiceAutoSyncService`, a hosted
  `BackgroundService` in `HoshiBot.Web` that runs the sync on startup then every 12h (~twice daily),
  meta.json-gated so between-season ticks are cheap no-ops. Runs in Web (not a Host Quartz job) because
  the sync service lives in Web and Web can't reference Discord/Quartz — mirrors `StfcSystemSyncService`.
- **Richer territory-metadata sync** (deferred) — the same `static_*.json` also carries per-region takeover
  windows (duration/start_hour/weekday), tier, neighbours, node/system links — richer than the
  current hardcoded `StfcTerritorySeedData` (single global weekday/time, tier-derived duration).
  Ingesting it would let the TC scheduler move off the single-weekday model to real per-region
  windows — a larger change touching `TerritoryCaptureScheduler`.

## Territory ownership auto-sync — ✅ done (2026-07-25)

Territory ownership (`StfcTerritoryOwnership`) now refreshes automatically instead of only via the
file-upload Import page: `StfcTerritoryOwnershipSyncService` fetches the live feed
`https://api.stfc.pro/stfc_territories` (a flat `[{server, territory, tag, region}]` array; the same feed
territory.lol uses) and hands it to the existing `ImportAsync` upsert. `TerritoryOwnershipAutoSyncService`
(hosted `BackgroundService` in `HoshiBot.Web`) runs it on startup then hourly, and a "Sync from stfc.pro"
button on `/manage/stfc/territory-ownership` triggers it manually. The file-upload Import page stays as a
fallback. Note: the old "stfc.pro `/api/` is robots-disallowed" caveat (see `StfcServerStatus`) was about
the main `stfc.pro` site — the **`api.stfc.pro` subdomain is open** (200, no robots restrictions), so the
seed-only `StfcServerStatus`/`StfcEventStatus` could later move to this same live source too.

## "Powered by" attribution for external-data features — ✅ Web done (2026-07-25)

Web admin surfaces backed by third-party data now show a small "Powered by {source}" credit (linked).
Driven by a **static registry keyed by domain entity type** (`HoshiBot.Domain/Attribution/`):
`PoweredBySource` (Name+Url), `PoweredBySources` (territory.lol / stfc.pro / stfc.space), and
`PoweredByRegistry.For(params Type[])` mapping the **16 external-data entities** to their source (deduped,
so a mix like Territory Capture's territory.lol + stfc.pro renders both). The `<PoweredBy For="…" />`
Shared component renders it; placed on the 15 Stfc catalog Index pages and the 11 external-data feature
editors (+ the Service Selection ExtraPage). Registry unit-tested.

Deferred:

- **Discord-facing credit** — the shared `EmbedBranding` footer is used by 30+ message types, so a
  per-source credit there would be global clutter; left out for now.
- **gilli.site** (`/api/events` + `/api/server-status`) — not wired anywhere yet; add a `PoweredBySources`
  entry + registry mapping when those notify features are built (one line each).

## Territory Capture "Services role" sync — DONE

Shipped as the standalone **Services Role Sync** feature (`GuildFeature.ServicesRoleSync`,
Alliance audience; depends on RankRoles + TerritoryCapture). When enabled for an alliance, it keeps
the TC `ServicesRole` assigned to exactly the alliance members who hold a rank role that grants the
in-game "Activate Services" permission — **Admiral or Commodore** (`RankRolesSettingKeys.AdmiralRole`
/ `CommodoreRole`), gated on the alliance member role. Full add/remove sync folded into
`TerritoryCaptureRoleSyncJob` (reads a fresh roster, so it always reflects the latest `RankRoleSyncJob`
result). The editor's Services-role and Member-role pickers are live shared views of the existing TC
`ServicesRole` setting and `GuildAlliance.MemberRoleId` (editing there or here is the same value) — the
feature owns no settings of its own.

## Role sync 403s: pre-check instead of failing per member — ✅ done (2026-08-06)

Discord's rate-limit guide has an **Invalid Request Limit**: 10,000 responses of 401/403/429 in any
10 minutes gets the IP temporarily banned from the API — for the whole bot, all guilds. The guide is
explicit about what it expects instead:

> 403 responses are avoided by **inspecting role or channel permissions** and by not making requests
> that are restricted by such permissions.

The nine role-touching jobs all run every 10 minutes — the same window Discord measures — and every
one of them catches `Forbidden` **inside** its per-member loop (`ExclusiveTierRoleSyncJob.SyncMemberAsync`,
`ConditionalRoleSyncJob`, `AllianceTagRoleSyncJob`, `NicknameSyncJob`, `PlayerLinkSyncJob`,
`NotificationRoleSyncJob`, `TerritoryCaptureRoleSyncJob`). So a single misconfiguration produces one
403 **per member, per job, per run**, indefinitely.

Measured against the live guilds (2026-08-06): EU 164 = 525 members, BASE OF SHADØW = 235,
Lost Falcons = 196 — 956 total. One role dragged above the bot's role in EU 164 alone is 525 invalid
requests per 10 minutes from one job. If the bot's own role is moved below the roles several jobs
manage, ~956 × 9 ≈ **8,600 per 10 minutes**, sustained — against a 10,000 ceiling. And the trigger is
the most common Discord admin mistake there is; `ConditionalRoleSyncJob`'s own comment notes it "has
bitten this community before on a legacy bot".

**The fix.** Two guild-level facts, resolved once per job run from the gateway cache (no REST calls),
both of which the permission declaration work already has the pieces for:

1. **Manage Roles** — if the bot's role doesn't have it, skip the guild entirely and report once via
   `NotifyAdminOfPermissionIssueAsync` (which now names the permission) instead of N 403s.
2. **Role hierarchy** — a role at or above the bot's highest position can never be assigned, whatever
   the permissions say (Administrator does **not** bypass role hierarchy). Comparing `RawPosition` is
   a per-role check, not per-member, and it is the actual cause of nearly all of these.

Suggested shape: pure comparison logic in Domain (`RoleSyncEligibility`, unit-testable), a thin
`RoleSyncGuard` in `HoshiBot.Discord` resolving it from `gatewayClient.Cache.Guilds`. **Fail open** —
if the guild, bot member or roles can't be resolved, behave exactly as today, so a bug in the guard
degrades to the current behaviour rather than silently stopping role sync (which would be worse than
the problem). Log every skip loudly for the same reason. `NicknameSyncJob` is the same story with
Manage Nicknames, plus the rule that nobody can rename the guild owner.

**This contradicted a stated rule**, and that rule was narrowed rather than kept: the comment on
`NotificationDispatcher.NotifyAdminOfPermissionIssueAsync` said resolving effective permissions
ourselves "would be easy to get wrong". That is fair for *channel* permissions (overwrite resolution,
category inheritance) but not for these two — guild-level permission bits and role positions are
simple, and Discord asks applications to check them. The rule kept is: don't pre-check channel
overwrites; do pre-check the guild-level facts. It now lives in CONTRIBUTING "Discord API limits".

**Shipped.** `RoleSyncEligibility` (Domain, pure + tested) holds the decisions; `PermissionGuard`
(singleton, 60s cache over the gateway cache — no API calls) resolves them per guild; all seven
role/nickname jobs check once per run instead of once per member, and report per guild through the
now-escalating admin throttle. `InvalidRequestTrackingHandler` wraps NetCord's request handler so the
rolling 10-minute invalid-request count is measured rather than estimated, and trips a process-wide
kill switch on the first 401 (a revoked token otherwise leaves every job firing 401s forever, since
NetCord stops the gateway on close code 4004 but never the REST client). A Quartz trigger listener
vetoes all jobs while that switch is set.

### Still open: the channel-level offenders

The same treatment has not been applied to the channel-based repeat-forever loops, which need
`ChannelAccessEvaluator`'s overwrite math rather than the simple guild-level facts:

- **`CommandBridgeRepublishJob`** — the worst of the lot: runs **every 5 seconds**, and on a 403 the
  `continue` skips the `RemoveRange` that would dequeue the row, so a stuck bridge re-fires forever.
  `CommandBridgeHubService.PublishAsync` compounds it by swallowing the failed edit and falling
  through to a send — two invalid requests every five seconds per stuck row. **The dequeue bug is
  worth fixing on its own, independently of any permission work.**
- **`AiChatIndexJob`** — up to ~2,000 `GetMessagesAsync` per guild per run (500 sources × 4 pages);
  each unreadable channel 403s every 20 minutes and no channel is ever marked bad.
- **`StfcNewsNotifyJob`** — never inserts its `StfcNewsPostGuildMessage` row on a 403, so the guild
  looks "missing" again and retries every 30 minutes forever.
- **`AbsenceReportRefreshJob`** — deliberately keeps the message id on a 403 (correct: it must not
  orphan a duplicate), which means the same failing edit every 15 minutes.

Also unfixed: `NotificationDispatcher.TrySendToChannelAsync` turns one 403 into **three** REST calls —
the failed send, a log-channel send, and an admin-channel send — and in a guild-wide permission gap
the latter two fail too. Only the third is throttled.

## Channel settings with no consumer — delete the columns and their pickers

Surfaced while building the per-feature permission declaration (`GuildFeaturePermissions`). Eight
configured channels have **no bot code reading them at all** — they exist only as pickers in the
admin UI, in `GuildSettingsSeedData`, and (until now) in the permission audit, which demanded
View + Send + Embed Links on every one of them:

- Seven `GuildAlliance` columns: `AllianceBoardingChannelId`, `RemindersAlliesChannelId`,
  `RulesDeChannelId`, `RulesEnChannelId`, `UserNotificationsChannelId`, `BotSupportChannelId`,
  `CommandStaffJobsChannelId`. Only the three Command Bridge columns are live.
- `GuildSettings.UserLogChannelId` (still rendered by `SettingsEditor.razor`).

They no longer appear in the audit. Remaining work: delete the columns, their pickers in
`Alliance/Settings.razor` / `SettingsEditor.razor`, and their seed rows, with an EF migration. Left
out of the permission rework itself because a schema migration doesn't belong bolted onto it.

Two more turned up in the same sweep and were decided at the time:

- **`AnnouncementsSettingKeys.RemindersChannel`** — a legacy-bot leftover (reminder pings for unread
  announcements, which are DMs now or will be). Removed outright: the key, the picker, the seed row
  and the catalog wording. Existing `GuildFeatureSettingSnowflakes` rows with `Key = 'RemindersChannel'`
  are now inert and can be deleted alongside the columns above.
- **`DiplomacySettingKeys.Channel`** — kept. Nothing reads it yet, but the feature is planned, so
  the slot is declared now rather than added later and forgotten.

## Contested player claims — admin approval queue

`/me` lets a member connect a player account themselves, but **blocks** a player already linked to a
Discord account outside their own account group (`PlayerLinkService.GetPlayerOwnersAsync`), since
claiming it would silently merge two people. The only way out today is self-service: prove the other
Discord account is yours via the OAuth link flow, and the player appears.

That leaves the genuine "someone else claimed my commander" case with nowhere to go. Add a **request
approval** path from the block message: file a review row (the `PlayerLinkReview` queue, or a sibling
of it) that an admin confirms or rejects on the Player Assignments page, which then does the link.
Deliberately not built with the first version — the block plus the account-linking escape hatch covers
the common case, and an approval queue nobody watches is worse than none.

## Web: hide "Manage" from members with no admin rights anywhere

The landing header and hero show a **Manage** button to every logged-in user. A plain member (no
Manage Server permission in any guild Hoshi is in, not a global admin) lands on `/manage` and sees an
empty dashboard — the button promises something they can't use. Resolve the same way the dashboard
does (`GuildAccessService.GetAccessibleGuildsAsync` + the `GlobalAdmin` policy) and hide it when the
result is empty; `/me` is the right destination for those users.

Needs care on cost: that check hits the user's OAuth guild list, so it must reuse
`DiscordUserGuildsService`'s existing 60s cache rather than firing a fresh call per page render, and
it renders in a layout that's on every public page.

## Slash commands — prune what the web admin replaced — ✅ done (2026-08-05)

Twelve of the thirteen application commands were deleted; `/hoshi-say` is the only one left. Each was
either a worse duplicate of a Command Bridge button or a Web page, or actively harmful
(`/set-my-alliance` let any member rewrite a shared catalog row; `/link-player` inserted a duplicate
`StfcPlayer` on a case-sensitive miss). `/shield-reminder-disable` was replaced by the terminate
button that every reminder DM already carried, and the `Create preview` message command by
🟩 🟨 🟥 🟦 reactions on the announcement draft channel (`AnnouncementDraftService`).

Correction to what this entry used to claim: deletions do **not** need a manual command
re-registration. `ApplicationCommandServiceManager` (NetCord source, `:55-79`) gathers the commands
from every registered service into one array and issues a single
`BulkOverwriteGlobalApplicationCommandsAsync`, so a command whose module is gone disappears from
Discord on the next start by itself.

## Localization: nothing verifies dynamic `Msg.WebEditor.*` keys against the catalog

`Msg.WebEditor.CardTitle/Label/Usage(lang, feature, settingKey)` builds
`Web.Editor.{feature}.{settingKey}.{kind}` at runtime, so a key the razor requests but the catalog
doesn't have compiles, tests green, and only shows up as a raw key on screen. That bit twice, both
from the same cause — the JSON was authored under a card's *display* name while the razor passes the
*storage* key: `TerritoryCapture.DigestInstructions` vs. `.Instructions`, and
`NicknameSync.AllianceTag`/`.ServerTag` vs. `.AllianceTagMode`/`.ServerTagMode` (both fixed
2026-07-31; the NicknameSync pair was found by sweeping for the rest of the class, not by anyone
seeing it).

A test would need to read `src/HoshiBot.Web/**/*.razor` and resolve the `*SettingKeys` constants,
which `HoshiBot.Domain.Tests` can't do from its own project reference — so either a small analyzer,
a CI script, or a Web-side test project. Until then, re-run the ad-hoc sweep after adding
setting-keyed cards. The same blind spot covers `Msg.WebFeature.Title/Description/ExtraTitle` and
`Msg.WebAudience.*`, which are enum-driven and therefore enumerable — those *could* be covered by a
plain Domain test today.

## AiChat: index-time `<t:unix>` timestamp resolution (searchable event dates)

`ResolveDiscordTimestamps` (AiChatService.Context.cs) rewrites Discord timestamp tokens to readable
dates at *prompt-build* time, so the model can read event dates/times in retrieved snippets without a
re-index. It does not make those dates *searchable*: the stored `Content` (and its FTS vector +
embedding) still holds the raw `<t:…>` tokens, so a query like "welches Event am 1. August" can't
FTS-match on the date. Resolving in `AiChatIndexService.RenderMessageText` instead (or in addition)
would fix that, but needs a full re-index of the ~750 existing token-bearing rows. Do it only if
date-keyword *search* is actually wanted.

## AiChat: latest-announcements block could still bury a post past the global cap

`BuildLatestAnnouncementsBlockAsync` fetches `LatestAnnouncementsFetchPerChannel` per Preferred
channel but then shows only the `LatestAnnouncementsMaxShown` newest *across all of them*. A very busy
Preferred channel (e.g. a general-announcements channel) can still push an authoritative
`official-announcements` notice past that global cap. If that recurs, switch to a per-channel
guaranteed slice (show the top-N of each channel) rather than a single global newest-first list.

## LLM prompts: finish the move to English

`MemberInterviewService.BuildInterviewPrompt` was translated to English on 2026-08-02 (the interview
opener next to it is an English constant the model translates per member). The rest of the prompt
surface is still German and was deliberately left alone in that change:

- `HoshiPersona.Describe` — shared by AiChat, `/hoshi-say` and the interview, so the interview prompt
  currently mixes a German persona block with English instructions. Models handle that fine, but it's
  the obvious next one. Watch the tone of German replies when you do: they currently come from a
  German persona, and an English persona + "Answer in German" can read slightly more translated.
- `AiChatService.Routing.cs`'s `GateSystemPrompt`/`RouterSystemPrompt` and the `MemoryExtractor` /
  `MemberNoteExtractor` prompts — these are *decision logic* whose behaviour was tuned by observation.
  A regression looks like "she went quiet", "she chimes in too often", or silently worse extractions,
  days later. Do them in their own change, one at a time, so a regression is attributable.
- `AnnouncementTranslator` — self-contained; easy whenever.

## Player name normalization: the remaining call sites

`PlayerNameKey` (Domain) + the persisted `StfcPlayer.NameKey` / `StfcAlliance.NameKey`/`TagKey` now
back the five paths where a name is searched or matched: the auto-link matcher, the player picker,
both conditional-role operand pickers, and the onboarding "type your name" modal. Eight more places
still compare names directly, and each is its own small failure:

- ~~**`/link-player`**, **`/set-my-alliance`**, **`/set-diplomacy`**~~ — resolved by deletion in the
  2026-08 slash-command cleanup. `/link-player`'s "insert a new `StfcPlayer` on a miss" was the worst
  of these (a live data-corruption path: typing `speed` where the catalog holds `Speed` created a
  duplicate ghost row with `ExternalId = 0`). Linking now goes through the `/me` page and the
  onboarding modal, both of which search by key.
- **Alliance-by-tag resolution in three importers** — `StfcPlayerImportService.cs:36,70`,
  `SeedExtensions.cs:386,393`, `StfcTerritoryOwnershipImportService.cs:31,65`. All case-sensitive
  `ToDictionary`, so a feed whose tag case differs from the catalog silently drops the player's
  alliance to null — which then shows up as wrong roles rather than as an import error.
- **Import rename detection** (`StfcPlayerImportService.cs:74`, `StfcAllianceImportService.cs:47`) —
  ordinal compare, so a pure case change counts as a rename and appends a `NameHistory` row.
- **`MemberInterviewInviteJob.RankByActivityAsync`** (`:130-136`) — matches `AiChatIndexedMessages
  .AuthorName` to `CommanderName.Of(member)` with an ordinal dictionary, so activity ranking misses
  anyone whose chat name differs in case, and splits anyone who renamed mid-window.

Note these want the *key*, not merely a `ToLower()`: the tag lookups in particular are comparing
catalog data to catalog data, where homoglyphs are exactly as likely as case differences.

## Slash commands: a review and cleanup pass — ✅ done (2026-08-05)

Flagged while doing the name normalization, and answered by deleting twelve of the thirteen commands
(see the section above) — the name-taking options that wanted normalizing went with them.

Two things worth keeping from it:

- **Autocomplete.** `StationHousingSystemAutocompleteProvider` was the codebase's only one and was
  deleted with `/raid`/`/shield-reminder`. If an autocompleting option is ever needed again, the
  model to copy is in git history (`src/HoshiBot.Discord/Alerts/`, removed 2026-08-05), not on disk.
- **A shadowing hazard.** Every module wraps its body in `SendDelayedEmbedAsync(… async () => …)`,
  and a pattern variable declared inside that lambda can shadow a command parameter of the same name
  with no warning — that is what made `/hoshi-say` ping the invoker instead of the chosen member
  (fixed 2026-08-04). `/hoshi-say` is now the only command, but the same lambda shape is all over the
  button/modal modules.
