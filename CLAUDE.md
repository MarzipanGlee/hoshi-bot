# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Architecture

`HoshiBot.slnx` has 8 projects (see [README.md](README.md) for the full table). The two
splits that matter most when deciding where new code goes:

- **`HoshiBot.Domain`/`HoshiBot.Data` vs. everything else** — anything that only needs
  `HoshiBotDbContext` + entity/enum types (no NetCord, no Quartz) belongs in `Data`, not
  `Discord`, even if it's Discord-bot-flavored logic. `GuildFeatureService` lives in `Data`
  specifically so `HoshiBot.Web` can use it without a `ProjectReference` to `Discord` (which
  would drag in `NetCord.Services`/`Quartz` — dependencies a web admin panel has no
  business carrying).
- **`HoshiBot.Discord` vs. `HoshiBot.Host`** — `Discord` is a plain library: every
  slash-command/button/modal/menu module, every per-feature service, every Quartz job.
  `Host` is the composition root: `Program.cs` wires up DI, Quartz triggers, the Discord
  gateway connection, and seeding, plus exactly one gateway handler of its own
  (`GuildSyncHandler`). `Host.AddModules(typeof(PingModule).Assembly)` explicitly scans
  `Discord`'s assembly for modules — this is a real assembly boundary NetCord.Hosting
  relies on, not just an organizational nicety.

German is the primary user-facing language for all bot-facing text (embeds, button
labels, error messages) — English is fine for code, comments, and the Web admin UI.

## Local dev environment (in transition, as of 2026-07-13)

Local development is moving from native Windows to **WSL2** (Ubuntu 26.04, `.NET 10` SDK
installed there) — running Docker Engine directly inside the WSL2 distro rather than Docker
Desktop (side-steps Docker Desktop's licensing requirement for larger orgs) and matching
production's actual Linux environment (the `docker/Dockerfile.*` base images) much more
closely than a Windows host does. If you're picking this up on a fresh WSL2 setup: put the
repo checkout inside the WSL2 filesystem itself (not under `/mnt/c/...`) — bind-mount and
build performance are much worse across that boundary.

A related, **not-yet-executed** follow-up discussed alongside this move: switching local dev's
database from SQLite to Postgres (via the `postgres` service already in `compose.yaml`, e.g.
`docker compose up postgres`) to close the gap between SQLite's EF Core provider limitations
(see the two SQLite gotchas right below) and what production actually runs. Check whether this
has happened before assuming either way — if `src/HoshiBot.Host/appsettings.Development.json`
/ `src/HoshiBot.Web/appsettings.Development.json` still have a `Data Source=...hoshibot.dev.db`
connection string and `"Database": {"Provider": "Sqlite"}`, it hasn't yet, and the
`EnsureCreated()` plus "delete `hoshibot.dev.db` after a schema change" workflow below is still
current. If it *has* happened, local dev needs real EF migrations like production does —
`EnsureCreated()` only applies to the SQLite path.

## Known gotchas

- **SQLite can't translate `DateTimeOffset` comparisons/ordering** in LINQ `Where`/`OrderBy`
  (production runs Postgres, which handles this fine). Materialize with `ToListAsync()`
  first, then filter/order client-side. Every place this bites has a comment explaining it
  — search for "SQLite's EF Core provider can't translate" before assuming a query is safe.
- **SQLite can't translate `ulong` (Discord snowflake `GuildId`/`DiscordUserId`/`ChannelId`/etc.)
  columns in ORDER BY either** — same family of gotcha as the one above, same fix
  (`NotSupportedException` locally; Postgres handles it fine). This bites in two ways: an
  initial query ordering by a `ulong` column, and — easy to miss — marking a `ulong`
  `PropertyColumn` `Sortable="true"` in a QuickGrid, which throws the moment someone clicks
  that column's header rather than on page load. Hit for real building the `Manage/Database/`
  debug pages (see below) — order by an `int`/`string` column instead (or don't order at all
  if none exists), and leave `ulong` columns non-sortable.
- **`EnsureCreated()` (SQLite dev path) only builds schema for a *new* file.** After adding
  entities or changing the schema, delete `hoshibot.dev.db` so it gets recreated.
- **EF Core migration scaffolding sometimes can't tell a rename from a drop+recreate.**
  Always open a scaffolded migration and check for `DropTable`/`DropColumn` where a rename
  was intended — rewrite by hand as `RenameTable`/`RenameColumn`/`RenameIndex` (+ raw SQL
  `ALTER TABLE ... RENAME CONSTRAINT ...` for PK/FK names) so production data survives.
- **NetCord component-interaction handlers always post a *new* message unless you
  explicitly return `InteractionCallback.ModifyMessage(...)`.** Returning a bare
  `InteractionMessageProperties`/`Task<InteractionMessageProperties>` is *always* wrapped
  as `InteractionCallback.Message(...)` (a new message) by the framework's result
  resolver — there's no auto-detection of "this is a follow-up step, edit in place."
  - `ModifyMessage` is only safe when the interaction's originating component lives on a
    message *this bot* created as ephemeral (a private wizard step) — never the shared,
    persistent Command Bridge hub message. A modal opened directly from a hub button (e.g.
    Shield Reminder's setup modal) must still post a *new* ephemeral message on submit;
    only modals opened from a component *within* an already-ephemeral wizard message are
    safe to `ModifyMessage`.
  - This also works for modal submissions: `ModalInteraction.Message` is populated when the
    modal was opened from a component, and Discord resolves `ModifyMessage` against that
    originating message with no explicit ID needed.
  - See `AbsenceButtonModule`/`AbsenceModalModule`/`AbsenceStringMenuModule` for the
    canonical example of a full wizard done right (entry point posts new, every follow-up
    step — including modal submits — edits in place).
- **Any interaction handler doing non-trivial work (DB writes, a Discord REST call, anything
  not near-instant) must ack immediately and edit afterward — never return a single response
  built at the end of the handler.** This is a hard requirement, not just a visual nicety:
  Discord interactions must be acknowledged within ~3 seconds or they become invalid, and a
  handler that does DB round-trips first (worse under concurrent load — e.g. `HoshiBot.Web`
  and `HoshiBot.Host` sharing the same dev SQLite file) risks the interaction expiring or
  double-acknowledging. Observed for real, live-tested: `RestException` "Unknown interaction"
  (404) and "Interaction has already been acknowledged" (400) — both went unnoticed by
  build/tests and only surfaced under real interaction latency. Concretely:
  `await Context.Interaction.SendResponseAsync(InteractionCallback.Message(EphemeralReply.Of("⏳ Processing...")))`
  immediately, do the slow work, then `await Context.Interaction.ModifyResponseAsync(m => ...)`
  with the real outcome — never a single `return` at the end.
  - The immediate ack (and its later edit) should be **ephemeral and personal** to the
    clicking user, kept **fully independent** of any shared/persistent message the
    component lives on. If the handler also needs to update that shared message, do it via
    a separate, plain `gatewayClient.Rest.ModifyMessageAsync(...)` call — never by reusing
    the interaction response to edit the shared message, which conflicts with the ephemeral
    ack/edit above and causes the same "already acknowledged" failure.
  - Legacy already did an ack-then-edit for two flows (Absences' "Abwesenheiten verwalten"
    and the Command Bridge hub's "Ungelesene Ankündigungen"), just not ephemeral; see
    `AbsenceButtonModule`/`CommandBridgeButtonModule` for that shape, and `StfcNewsButtonModule`/
    `StfcNewsModalModule` for the full ephemeral version.
- **Guild nicknames often carry an alliance tag prefix** (e.g. `[LF] PlayerName`). Use
  `CommanderName.Of(Context.User)` (strips a leading `[...]` bracket group, ported from
  legacy's `$defs.RegEx.MemberName` regex) instead of `Context.User.Username` whenever
  building a "Commander {name}, ..." message — the raw Discord username/nickname is not
  the display name legacy always showed.
- **A Quartz job that creates a row the first time it sees something (rather than only ever
  diffing pre-seeded rows) needs `[DisallowConcurrentExecution]`.** `WithSimpleSchedule()
  .RepeatForever()` fires an immediate first run at scheduler start; if that first run is slow
  enough (an HTTP fetch, a per-guild member scan), a second scheduled tick can start before it
  commits, and both can see "no row yet" for the same natural key and collide on a unique
  constraint. Hit for real with `StfcClientReleaseNotifyJob` and `StfcNewsNotifyJob` (both
  insert a new row on first detection) — `ServerStatusNotifyJob`/`InfiniteIncursionsNotifyJob`
  don't need this since they only ever update rows a seeder already created.
- **A bare `HttpClient` with no User-Agent gets 403'd by some external sites' bot protection**
  — hit for real against `startrekfleetcommand.com`'s WordPress feed (a plain
  `HttpClient.GetStringAsync` failed; the same URL fetched fine via a tool that sends a
  realistic browser User-Agent). Register a realistic User-Agent
  (`AddHttpClient(name, client => client.DefaultRequestHeaders.UserAgent.ParseAdd(...))`) for
  any named `HttpClient` that hits a third-party site, not just Discord's own API.
- **A dedicated Hoshi Bot Discord test guild exists for live end-to-end testing** — the bot is
  already a member, with a channel category and admin channel/role already set up. Its ID and
  the seeder that provisions its `GuildSettings` row (`DefaultChannelCategoryId`, etc.) live in
  `SeedHoshiTestGuildSettingsIfEmptyAsync` (`HoshiBot.Data/ServiceCollectionExtensions.cs`) —
  use this guild for real interaction testing instead of the real "Lost Falcons" production
  guild. Note: Lost Falcons' own `AdminChannelId` currently has a real permission problem (the
  bot can't post there — a pre-existing Discord-side config issue in that production guild,
  not a code bug, and not affecting the test guild), so don't be surprised if a notify job logs
  `Forbidden` for Lost Falcons specifically.
- **Running `HoshiBot.Web` and `HoshiBot.Host` at the same time against the same dev
  `hoshibot.dev.db`** works (SQLite allows concurrent readers/writers) but adds real lock
  contention — worth knowing when diagnosing why an interaction handler is slower than
  expected in dev specifically (see the ack-then-edit gotcha above for why that matters).
- **Thread removal must never be a general-purpose user command.** An earlier `/close-thread`
  slash command (letting anyone with `ManageThreads` mark *any* thread for removal) was
  deliberately deleted — thread removal must only ever be a button/action a specific feature
  attaches to a thread it owns and understands the lifecycle of (e.g. a "close ticket" button
  once that feature exists). `ThreadRemovalRequest`/`ThreadCleanupJob` (the queue + cleanup
  job) are legitimate generic infrastructure and were kept — currently unused since nothing
  produces rows for them — but a future thread-owning feature should wire a feature-specific
  button into that same queue, not reintroduce a standalone command.
- **A plain HTML `<select>` skips a `disabled` placeholder `<option>` when resolving its
  default selection** — the browser jumps to the next enabled option instead of showing the
  disabled one as selected. If a placeholder/sentinel option needs to display as the default
  until a real choice is made, leave it a normal selectable option (it can still be
  functionally inert downstream via its value) — confirmed via screenshot on `RolePicker.razor`,
  don't assume `disabled` alone achieves "shown but not a real choice."
- **Bootstrap 5 utility classes (`.d-flex`, `.gap-3`, `.d-none`, etc.) all compile `!important`.**
  Before converting a custom CSS declaration to the matching utility class, check whether that
  same property is overridden elsewhere by a custom `@media` query (this app has non-standard
  641px/900px breakpoints tied to the sidebar's own collapse point, not Bootstrap's sm/md/lg),
  an inline `style` toggle, or a `:hover`/state selector — if that override isn't itself
  `!important`, the utility class silently and permanently defeats it regardless of source
  order. Hit for real converting `MainLayout.razor.css` to Bootstrap utilities (broke the
  sidebar's `@media(min-width:641px)` row/column layout twice in one sitting).
- **BootstrapBlazor's `<Collapse>` has no `ChildContent` parameter** — only a named
  `CollapseItems` RenderFragment. Nesting `<CollapseItem>` directly as `<Collapse>...</Collapse>`
  child content compiles cleanly but silently renders nothing; wrap explicitly:
  `<Collapse><CollapseItems>...</CollapseItems></Collapse>`.
- **EF Core can't translate `localList.Any(x => x.ForeignId == entity.Id)`** — a lambda
  comparing against a local `List<TEntity>` of entity objects (not primitives) throws
  `InvalidOperationException: could not be translated`, and inside a Blazor Server circuit
  this is swallowed entirely (no console output, no error UI) — a cascading dropdown or
  filtered list just silently stays empty. Fix: precompute
  `var ids = localList.Select(x => x.ForeignId).ToHashSet();` first, then
  `.Where(e => !ids.Contains(e.Id))` — a primitive-typed `HashSet.Contains` translates fine.
- **BootstrapBlazor's bundled CSS truncates `.form-check-label` text** (`white-space: nowrap;
  overflow: hidden`, fixed width) whenever the wrapping element has both `form-check` AND
  `form-switch` classes together — invisible until a switch gets a long label. Fix: drop the
  `form-check` class and keep only `form-switch` (the input's own classes still apply the
  switch styling) — cleaner than fighting it with a component-scoped `!important` override.

## Conventions

- Every real (non-ephemeral-wizard-step) bot message shares the same author/footer via
  `EmbedBranding.BuildAuthorAsync`/`BuildFooter` — don't hand-roll embed branding.
- Per-guild feature toggles (`GuildFeature` enum + `GuildFeatureService`, in
  `HoshiBot.Data`) gate three layers for every toggleable feature: the Command Bridge hub
  button (hidden if disabled), the slash command (re-checked, since Discord can send stale
  interactions from an unrefreshed hub message), and any related Quartz job (skip disabled
  guilds so no notifications go out for a paused feature). When adding a new toggleable
  feature, check all three — it's easy to guard the slash command and forget the hub
  button or the job.
- Run `dotnet format --verify-no-changes` before committing — there's no CI here yet, so
  this is the only formatting check in place.
- **HoshiBot.Web UI: prefer BootstrapBlazor components over hand-rolled HTML.** The package
  is already referenced and its bundle already loaded in `App.razor` — when building new UI,
  reach for its components first (`<Select>`/`<AutoComplete>` instead of a plain
  `<select>`, `<Collapse>` instead of a hand-rolled checkbox/label toggle, etc.) rather than
  writing the plain-HTML equivalent from scratch.
- **Deciding CRUD vs. read-only for a new admin page**: does anything else (a Quartz job, a
  Discord command, another Web page) already write to this table automatically? If yes, it's
  job/Discord-managed — read-only (`StfcServerStatus`/`StfcEventStatus`/`StfcClientRelease`
  and everything under `Manage/Database/`). If nothing else writes to it, full CRUD is
  correct (`StfcRegion`/`Server`/`Alliance`/`Territory`, the STFC Discord-invite and
  Territory Ownership/Neighbour pages). `Manage/Database/` specifically is a debug-only
  section (plain QuickGrid Index pages, no Create/Edit/Delete) for tables with zero admin
  visibility anywhere else, raw or curated — check there before assuming a table needs a
  brand new page; it might already have a read-only one.
- **Push Discord/infra API calls (`RestClient`, `IMemoryCache`, etc.) into a service, even for
  a single caller.** A Razor component injecting these directly to make Discord REST calls
  should move that logic into the relevant service (e.g. `DiscordGuildDataService`) regardless
  of whether the call is duplicated elsewhere — this is about keeping components as UI/state
  glue and infra calls in a service layer, not about deduplication. Still use judgment on
  behavior-preserving extraction (a method's semantics may need a new parameter/overload
  rather than reusing an existing one with different behavior for its other callers).

## Collaboration notes

- **Get explicit sign-off (screenshot + approval) before committing/pushing any visual/styling
  change to `HoshiBot.Web`** — don't stage and commit right after your own local
  verification passes. A past NotFound-page styling fix looked correct in a local screenshot
  but the user's own test showed it still broken; the deeper issue was committing before they
  got a chance to look at all. Backend logic changes (auth, routing, job behavior) already
  verified via curl/build/test don't need this extra gate — this is specific to visual changes.
- **When the user shows concrete real-world evidence (a screenshot of actual Discord/browser
  state) that contradicts a conclusion drawn from reading code, trust the evidence and dig
  deeper — don't just restate the code-based analysis.** Static code analysis shows what code
  currently does, not whether that path was ever actually exercised for real; a live
  screenshot is stronger evidence of intent than an untested code path. Concretely: this once
  correctly surfaced a real bug (RoE Violations wired to a Forum channel that the current code
  couldn't actually create private threads on).
- **Before "fixing" what looks like an unintended regression in a pre-commit `git diff`, ask
  rather than silently reverting it.** A diff shows *what* changed, not *why* or *by whom* —
  a removal that looks like collateral damage from your own earlier edits may be a deliberate
  change the user made themselves (confirmed happening once with an intentionally-removed CSS
  rule). State the finding and ask before restoring it.
