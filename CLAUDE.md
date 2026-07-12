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

## Known gotchas

- **SQLite can't translate `DateTimeOffset` comparisons/ordering** in LINQ `Where`/`OrderBy`
  (production runs Postgres, which handles this fine). Materialize with `ToListAsync()`
  first, then filter/order client-side. Every place this bites has a comment explaining it
  — search for "SQLite's EF Core provider can't translate" before assuming a query is safe.
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
