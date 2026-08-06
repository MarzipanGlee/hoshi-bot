# Contributing to Hoshi Bot

This document is the rulebook for anyone writing code or fixing bugs in this repository.
[README.md](README.md) covers *how to set up and run* the project (local dev, secrets,
deployment, EF migration commands) — this document covers *how to write code that fits*.

A phased refactoring effort is tracked in
[docs/refactoring-plan.md](docs/refactoring-plan.md); several rules below reference base
classes and helpers that plan introduces. Follow the rule in spirit even where the helper
does not exist yet.

## Getting started

1. Follow the "Local development" section of [README.md](README.md) (.NET 10 SDK, Docker
   for the local PostgreSQL, user-secrets for tokens).
2. On Windows, develop inside **WSL2** with the repo checkout in the WSL2 filesystem
   itself (not under `/mnt/c/...`) — it matches the production Linux environment and
   bind-mount/build performance across the Windows boundary is much worse. Docker Engine
   directly inside the distro works fine (no Docker Desktop needed); add your user to the
   `docker` group.
3. Before committing, run:

   ```bash
   dotnet build
   dotnet test
   dotnet format --verify-no-changes
   ```

## Architecture — where code goes

`HoshiBot.slnx` has 8 projects (see the table in [README.md](README.md)). The two splits
that matter most when deciding where new code goes:

- **`HoshiBot.Domain`/`HoshiBot.Data` vs. everything else** — anything that only needs
  `HoshiBotDbContext` + entity/enum types (no NetCord, no Quartz) belongs in `Data`, not
  `Discord`, even if it's Discord-bot-flavored logic. `GuildFeatureService` lives in
  `Data` specifically so `HoshiBot.Web` can use it without a `ProjectReference` to
  `Discord` (which would drag in `NetCord.Services`/`Quartz` — dependencies a web admin
  panel has no business carrying). `HoshiBot.Web` must never reference `HoshiBot.Discord`.
- **`HoshiBot.Discord` vs. `HoshiBot.Host`** — `Discord` is a plain library: every
  slash-command/button/modal/menu module, every per-feature service, every Quartz job.
  `Host` is the composition root: `Program.cs` wires up DI, Quartz triggers, the Discord
  gateway connection, and seeding, plus exactly one gateway handler of its own
  (`GuildSyncHandler`). `Host.AddModules(typeof(CommanderName).Assembly)` explicitly scans
  `Discord`'s assembly for modules — this is a real assembly boundary NetCord.Hosting
  relies on, not just an organizational nicety.

File placement rules:

- **Discord:** every feature lives in its own `<Feature>/` folder (service + its
  Button/Modal/Menu/message-command modules together). No feature files at the project
  root — only genuinely cross-cutting helpers (`EmbedBranding`, `EphemeralReply`,
  `InteractionResponseExtensions`, `CommanderName`, `HoshiPersona`) stay there.
- **Web:** non-routable components go in `Components/Shared/` (or the feature folder they
  belong to), never loose under `Pages/`. Area-wide authorization is declared once in the
  area's `_Imports.razor` (`@attribute [Authorize(Policy = ...)]`), never hand-rolled per
  page.
- **DI:** `HoshiBot.Data` services are registered through one shared extension method in
  `Data/ServiceCollectionExtensions.cs`, not by per-composition-root lists in `Host` and
  `Web` (drift between the two roots has caused real bugs).

## Language policy

- **Discord-facing text is localized via the message catalog** — never hardcode a
  user-facing string. Every message/embed/button/modal string lives as a key in
  `src/HoshiBot.Domain/Localization/Locales/{en,de}.json` with a typed accessor in
  the matching `Msg.<Feature>.cs`; add every enabled locale in the same commit (the
  `MessageCatalogTests` parity suite fails otherwise). Render with a **resolved**
  `Language`, never a literal: ephemeral/modals → the acting user
  (`LanguageResolver.ForUserAsync` with the interaction locale), public posts → the
  owning scope (`ForAlliance/ForAudience/ForGuildAsync`; `GuildAlertChannel`
  fan-outs via `NotificationDispatcher`'s `Func<Language,…>` overloads), DMs and
  user-dedicated threads → the addressee, admin notifications → the guild language.
  See [docs/localization-plan.md](docs/localization-plan.md) for the full rules and
  the add-a-locale recipe.
- **LLM prompt text is not catalog material** — prompts stay in code (English or
  German as the feature requires) and carry a dynamic "Answer in {language}."
  instruction where the reply is user-facing.
- **The Web admin UI is localized the same way**, for the pages guild/alliance admins
  actually reach: keys live in
  `src/HoshiBot.Domain/Localization/Locales/Web/{en,de}.json` (all `Web.`-prefixed,
  merged into the same catalog — see `Msg.Web*.cs`), rendered with the
  `[CascadingParameter] Language Lang` every layout provides via `WebRequestLanguage`
  (the signed-in user's explicit choice, else `Accept-Language`, else English). The
  operator-only areas (`Manage/Database`, `Manage/Bot`, `Manage/Stfc` — all
  `GlobalAdmin`-gated) and the landing page's legal disclaimer stay English by design;
  everything else new needs both locales in the same commit.
- **Everything else is English**: code, comments, commit messages, docs, and
  slash-command canonical names/descriptions.

## Coding conventions

### Discord / Host / Data

- Build every branded embed via
  `EmbedBranding.BuildBrandedAsync(guildId, description, color?, title?)` (set
  `Fields`/etc. on the returned embed), never a hand-assembled `EmbedProperties` with
  Author/Footer. **Every user-facing interaction reply is a branded embed, not plain
  text**: `EmbedBranding.EphemeralAsync(guildId, text, …)` for a fresh ephemeral
  reply/guard, `EmbedBranding.BrandedEditAsync(guildId, text)` for the result step of an
  ack-then-edit handler, and `Interaction.SendDelayedEmbedAsync(embedBranding, guildId,
  work)` for the ack-immediately-then-edit confirmation flow. Plain text is only for the
  transient `⏳ Processing...` ack.
- Feature-gate an interaction with
  `GuildFeatureService.EnsureEnabledAsync(guildId, feature, lang)` (returns the disabled
  message or null).
- Per-guild feature toggles (`GuildFeature` enum + `GuildFeatureService`) gate **three
  layers** for every toggleable feature: the Command Bridge hub button (hidden if
  disabled), the slash command (re-checked, since Discord can send stale interactions
  from an unrefreshed hub message), and any related Quartz job (skip disabled guilds).
  When adding a toggleable feature, check all three — it's easy to guard the slash
  command and forget the hub button or the job.
- When a new feature genuinely can't do anything without another feature configured (not
  just "nice to combine with"), declare it in `GuildFeatureDependencies.Of`
  (`HoshiBot.Domain`) — that's what makes the Features page's dependency hint/badge show
  up. If the dependency is only usable from a subset of audiences, verify
  `GetDependencyStatesAsync` actually resolves via one of the dependency's
  genuinely-enabled audiences (see `IFeatureModule.cs`).
- Use `CommanderName.Of(Context.User)` (strips a leading `[TAG]` bracket group) instead
  of `Context.User.Username` whenever building a "Commander {name}, ..." message — guild
  nicknames often carry an alliance-tag prefix.
- Register Quartz jobs with the `AddSimpleJob<T>(interval)` / `AddCronJob<T>(cron)` local
  helpers in `Host/Program.cs`. Per-guild jobs use the shared per-guild runner (see
  refactoring plan) so the enabled-guild loop and per-guild error handling aren't
  re-rolled. Seeders live in `Data/Seeding/SeedExtensions.cs` and open with `WithDbAsync`.
- A module's `IsConfiguredAsync` must read via the `FeatureModuleContext` helpers
  (`context.GetSnowflakeAsync`/`GetTextAsync`/`HasAlertChannelAsync`/
  `HasFeatureChannelAsync`/`IsEnabledAsync`), **not** `context.Settings`/
  `context.DbFactory` directly — that's what lets the Features page serve them all from
  one `FeatureSettingsSnapshot` instead of ~100 queries.

### Web (Blazor)

- **Prefer BootstrapBlazor components over hand-rolled HTML.** The package is referenced
  and its bundle loaded in `App.razor` — reach for `<Select>`/`<AutoComplete>` instead of
  a plain `<select>`, `<Collapse>` instead of a hand-rolled toggle, etc.
- **Reach for the shared components/helpers before hand-rolling their pattern**
  (re-inlining any of them is a regression):
  - Guild/alliance admin pages: `GuildAdminPages`/`AllianceAdminPages`
    (`Components/Shared`) are the single source for the admin-page list — the sidebar nav
    group *and* the overview card grid both iterate them; add a page there, never in two
    spots. The "no alliance linked" hint is `AllianceLinkRequiredHint`. Page bodies wrap
    in `<AuthorizedGuildView Authorized="Authorized">`. Shortcut/overview cards use
    `SettingsCard` with `HeaderContent` + `ChildContent` (footers are for
    actions/buttons only). A settings card that's just one picker whose title equals its
    label uses `<ChannelPicker/RolePicker CardTitle="X" …/>` (the picker self-wraps the
    card).
  - STFC CRUD pages: read-only QuickGrid list pages `@inherits DbContextPageBase` and
    bind `Context.<DbSet>`. Create/Edit form fields use `<FormField>`; Delete pages use
    `<DeleteConfirmation>`; single-file JSON imports use `<ImportForm TResult=…>`.
  - Feature editors: every editor `@inherits FeatureEditorBase`; the enable toggle is
    `<FeatureEnableSwitch>`; a set of tier roles uses `<RoleTierEditor>` +
    `RoleTierSpec`; load/save individual settings via the base-class setting helpers
    rather than spelling out the full
    `(GuildId, Feature, ResolvedAudience, GuildAllianceId, key)` tuple per call site.
  - A feature's auxiliary admin page (not its main editor) belongs in that feature's own
    `Features/{Name}/` folder, registered via `IFeatureModule.ExtraPages` — never as a
    stray sibling directly under `Manage/Guild/`. Routing
    (`FeatureExtraPageHost.razor`) and the breadcrumb both derive from that declaration.
    Link to it from the editor with `<ExtraPageLink>`, not a hand-rolled `<a>`.
- **Destructive inline actions** (a table-row "Delete"/"Remove", a "Forget") use
  `btn btn-sm btn-danger`. A dedicated full-page delete *confirmation* button
  (`Manage/Stfc/**/Delete.razor`) is the one exception — no `-sm`. Don't introduce
  one-off styles (`btn-outline-danger` etc.) for the same kind of action.
- **CRUD vs. read-only for a new admin page**: does anything else (a Quartz job, a
  Discord command, another Web page) already write to this table automatically? If yes,
  it's job/Discord-managed — read-only. If nothing else writes to it, full CRUD is
  correct. `Manage/Database/` specifically is a debug-only section (plain QuickGrid
  Index pages, no Create/Edit/Delete) for tables with zero admin visibility anywhere
  else — check there before assuming a table needs a brand-new page.
- **Push Discord/infra API calls (`RestClient`, `IMemoryCache`, etc.) into a service,
  even for a single caller** (e.g. `DiscordGuildDataService`) — components stay UI/state
  glue. Use judgment on behavior-preserving extraction (a method's semantics may need a
  new parameter/overload rather than reusing an existing one with different behavior for
  its other callers).
- **Page spacing/colours are CSS, not per-page utilities** — title/subtitle/section
  spacing lives in `wwwroot/css/site.css` (`article h1`, `article h1 + p`, `article h2`,
  `.card h2`); don't add per-page `mt-*`/`mb-*` on headings. Card icon/subtitle colours
  are `.card-icon`/`.card-subtext` classes (not inline styles); sidebar item padding
  lives on `.nav-item`.

## Design rules

- **Member-touching features are opt-in.** The bot must not proactively DM/ping members
  by default. When a feature *could* reach out to members, split it: do the
  mechanical/automatic work silently, surface anything unresolved in a Web admin table
  for manual handling, and put member-facing DM outreach behind a separate, opt-in
  feature toggle (off by default). Canonical example: `GuildFeature.PlayerLink` (silent
  matching + admin table) vs. the distinct opt-in `GuildFeature.MemberOnboarding` (DM
  outreach).
- **Thread removal must never be a general-purpose user command.** An earlier
  `/close-thread` command was deliberately deleted — thread removal is only ever a
  button/action a specific feature attaches to a thread it owns and understands the
  lifecycle of. `ThreadRemovalRequest`/`ThreadCleanupJob` are legitimate generic
  infrastructure (currently without a producer) — a future thread-owning feature should
  wire a feature-specific button into that queue, not reintroduce a standalone command.

## Discord API limits

- **Discord bans the IP after 10,000 invalid responses in 10 minutes, and 401, 403 and
  429 all count.** It is a temporary Cloudflare ban on the whole bot, every guild — not a
  per-route throttle. The bot measures its own rate: `InvalidRequestTrackingHandler` wraps
  NetCord's request handler and logs a warning once the rolling 10-minute count crosses
  10% of the ceiling, so a guild that starts generating volume says so before the ban does.
- **Never hand-roll 429 handling — NetCord owns it completely.** It keeps per-route and
  global buckets, *sleeps before sending* when one is exhausted rather than firing and
  failing, honours `Retry-After`, and treats `X-RateLimit-Scope: shared` separately. Do
  know that its retry is **unbounded** by default: a pathological route retries forever,
  and the only escapes are the `CancellationToken` every REST call takes and
  `RestRequestProperties.RateLimitHandling = NoRetry`.
- **Pre-check *guild-level* permissions before a call that would 403; do NOT pre-check
  channel permissions.** Discord asks for this explicitly ("403 responses are avoided by
  inspecting role or channel permissions"). The distinction is what makes it safe: a
  guild-level bit and a role's position are simple facts, and `PermissionGuard` reads both
  from the gateway cache for free. Channel access needs overwrite resolution and category
  inheritance — being wrong there silently drops a message, so channel 403s stay reactive
  (`NotifyAdminOfPermissionIssueAsync`). Note **Administrator does not bypass role
  hierarchy**, and nobody can rename the guild owner — `RoleSyncEligibility` holds those.
- **A guard must fail open.** `PermissionGuard.For` returns null when it cannot work the
  answer out, and every caller must then behave exactly as it would have. A wrong "no"
  stops roles syncing silently, which is worse than the 403s the guard exists to prevent.
- **A `catch` inside a per-item loop repeats forever unless the item's state advances.**
  This is the shape that turns one misconfiguration into thousands of invalid requests:
  the loop swallows the failure, nothing is marked done, and the next run does it all
  again. Good precedent: `TerritoryCaptureDigestService.SweepExpiredMessagesAsync` removes
  its row even when the delete fails. Bad precedent: `CommandBridgeRepublishJob` skips the
  dequeue on failure and re-fires every 5 seconds forever.
- **Report a permission failure per guild, not per member**, and let the throttle escalate
  (`NotifyAdminOfPermissionIssueAsync`: immediately, then 10 min, 30 min, hourly). Call
  `NotificationDispatcher.ClearPermissionIssue` when the thing works again so a problem
  that returns is reported at once instead of resuming at the back of the backoff.
- **Use `ex.Error?.Code` for Discord's numeric error codes** (50013 = Missing Permissions),
  not just the HTTP status — a 403 for a missing permission and a 403 for something else
  want different handling. `Error` is **nullable**: it is null whenever the body was not
  parseable JSON.
- The measured analysis behind all of this — which jobs, how many members, how close to the
  ceiling — is in [docs/backlog.md](docs/backlog.md) under "Role sync 403s"; don't
  re-derive it here.

## Known gotchas

- **NetCord component-interaction handlers always post a *new* message unless you
  explicitly return `InteractionCallback.ModifyMessage(...)`.** Returning a bare
  `InteractionMessageProperties` is always wrapped as `InteractionCallback.Message(...)`
  (a new message) by the framework's result resolver — there's no auto-detection of
  "this is a follow-up step, edit in place."
  - `ModifyMessage` is only safe when the interaction's originating component lives on a
    message *this bot* created as ephemeral (a private wizard step) — never a shared,
    persistent hub message. A modal opened directly from a hub button must still post a
    *new* ephemeral message on submit; only modals opened from a component *within* an
    already-ephemeral wizard message are safe to `ModifyMessage`.
  - This also works for modal submissions: `ModalInteraction.Message` is populated when
    the modal was opened from a component, and Discord resolves `ModifyMessage` against
    that originating message with no explicit ID needed.
  - See `AbsenceButtonModule`/`AbsenceModalModule`/`AbsenceStringMenuModule` for the
    canonical wizard done right (entry point posts new, every follow-up step edits in
    place).
- **Any interaction handler doing non-trivial work (DB writes, a Discord REST call,
  anything not near-instant) must ack immediately and edit afterward — never return a
  single response built at the end of the handler.** Discord interactions become invalid
  after ~3 seconds; observed live: `RestException` "Unknown interaction" (404) and
  "Interaction has already been acknowledged" (400), invisible to build/tests.
  Concretely: send the `⏳ Processing...` ack immediately, do the slow work, then
  `ModifyResponseAsync` with the real outcome — `Interaction.SendDelayedEmbedAsync`
  wraps this flow.
  - The ack (and its edit) is **ephemeral and personal** to the clicking user, fully
    independent of any shared/persistent message the component lives on. If the handler
    also needs to update that shared message, do it via a separate plain
    `gatewayClient.Rest.ModifyMessageAsync(...)` call — never by reusing the interaction
    response, which causes the "already acknowledged" failure.
- **A Quartz job that creates a row the first time it sees something needs
  `[DisallowConcurrentExecution]`.** `WithSimpleSchedule().RepeatForever()` fires an
  immediate first run at scheduler start; if that run is slow, a second tick can start
  before it commits and both collide on a unique constraint. Hit for real with
  `StfcClientReleaseNotifyJob` and `StfcNewsNotifyJob`; jobs that only update pre-seeded
  rows don't need it.
- **A bare `HttpClient` with no User-Agent gets 403'd by some external sites' bot
  protection** (hit for real against `startrekfleetcommand.com`). Register a realistic
  User-Agent via `AddHttpClient(name, client => client.DefaultRequestHeaders.UserAgent
  .ParseAdd(...))` for any named client that hits a third-party site.
- **EF Core migration scaffolding sometimes can't tell a rename from a drop+recreate.**
  Always open a scaffolded migration and check for `DropTable`/`DropColumn` where a
  rename was intended — rewrite by hand as `RenameTable`/`RenameColumn`/`RenameIndex`
  (+ raw SQL `ALTER TABLE ... RENAME CONSTRAINT ...`) so production data survives. See
  README "EF Core migrations" for the workflow.
- **EF Core can't translate `localList.Any(x => x.ForeignId == entity.Id)`** — a lambda
  comparing against a local `List<TEntity>` of entity objects throws
  `InvalidOperationException`, and inside a Blazor Server circuit this is swallowed
  entirely (a cascading dropdown just silently stays empty). Precompute
  `var ids = localList.Select(x => x.ForeignId).ToHashSet();` and use `ids.Contains`.
- **A plain HTML `<select>` skips a `disabled` placeholder `<option>` when resolving its
  default selection** — if a placeholder must display as the default, leave it a normal
  selectable option and keep it functionally inert via its value.
- **Bootstrap 5 utility classes (`.d-flex`, `.gap-3`, `.d-none`, …) all compile
  `!important`.** Before converting custom CSS to a utility class, check whether that
  property is overridden by a custom `@media` query (this app has non-standard
  641px/900px breakpoints tied to the sidebar), an inline `style` toggle, or a state
  selector — a non-`!important` override is silently defeated regardless of source order.
- **BootstrapBlazor's `<Collapse>` has no `ChildContent` parameter** — only a named
  `CollapseItems` RenderFragment. Direct child content compiles cleanly but renders
  nothing; wrap explicitly: `<Collapse><CollapseItems>...</CollapseItems></Collapse>`.
- **BootstrapBlazor's bundled CSS truncates `.form-check-label` text** whenever the
  wrapping element has both `form-check` and `form-switch` classes — drop `form-check`
  and keep only `form-switch`.
- **`deploy.sh`'s migration step always prints `Cannot load library libgssapi_krb5.so.2`
  — ignore it.** Npgsql probes for Kerberos support at startup and the migrator image
  doesn't ship that library. The run continues and reports the real outcome on the next
  line.
- **NetCord's NuGet XML docs are incomplete** — existing members (e.g.
  `RestClient.GetUserAsync`) are missing from them. When an API seems absent, consult
  the NetCord sources before concluding it doesn't exist.
- **Discord timestamp tokens (`<t:UNIX>` / `<t:UNIX:style>`) are opaque to the AI model.**
  A Discord *client* renders them as localized dates, but in prompt text they're just
  integers the model can neither read nor reason about — and STFC event announcements put
  every date/time in these tokens, so a retrieved announcement arrives date-blind (Hoshi
  once insisted she had "no confirmed date" for an event whose date sat right there as a
  token). Any new AiChat prompt block that injects indexed/live message content must run it
  through `AiChatService.ResolveDiscordTimestamps(...)`, which rewrites the tokens to
  readable local dates. The one deliberate exception is the Territory-Capture facts block,
  which emits raw `<t:…:t>` on purpose *and* tells the model to pass them through verbatim
  so Discord localizes them per reader — do that only when the block's instruction says so.

## Database & migrations

Commands and the apply workflow are in README ("EF Core migrations" and "Local
development"). Rules:

- Migrations are applied via `HoshiBot.Migrator`, never automatically by bot/web.
- Always inspect a freshly scaffolded migration for bogus drop/recreate (see gotcha
  above) before committing it.
- After adding a migration, re-run the migrator against your local DB.

## Testing

- `dotnet test` runs the unit tests (`tests/HoshiBot.Domain.Tests`). New pure logic in
  `Domain` should come with tests.
- Live end-to-end testing happens in the dedicated Hoshi Bot **test guild** — the bot is
  already a member and `SeedHoshiTestGuildSettingsIfEmptyAsync`
  (`HoshiBot.Data/Seeding/SeedExtensions.cs`) provisions its `GuildSettings` row.
  Never test against the production guild.

## Workflow

- Day-to-day work lands on the `dev` branch; `main` is the release branch (PRs target
  `main`).
- Deferred ideas, follow-ups, and known bugs go into [docs/backlog.md](docs/backlog.md)
  — versioned and visible to everyone, rather than scattered in personal notes.
- Run `dotnet format --verify-no-changes` before committing — until CI lands (see
  refactoring plan, Phase 0), this is the only formatting check in place.
