# Refactoring plan

A phased plan to clean up the repository: deduplicate code (including Razor/HTML markup),
normalize project and file structure, reset the EF Core migration history, and localize
Discord-facing text. Written 2026-07-26 from a full-repo review; execute phases in order —
each phase leaves the repo green (`dotnet build`, `dotnet test`,
`dotnet format --verify-no-changes` all pass at every stopping point).

## Goals

1. **One convention per pattern** — every repeated pattern (CRUD page, feature setting,
   nav entry, per-guild job, notify job, loading placeholder, …) has exactly one shared
   implementation; hand-rolled copies are regressions.
2. **Dedup includes markup** — Razor/HTML boilerplate counts as duplication just as much
   as C#.
3. **Clean placement** — files live where the architecture says they belong
   (see [CONTRIBUTING.md](../CONTRIBUTING.md) "Architecture — where code goes"):
   feature folders in `Discord`, data-only services in `Data`, non-routable components
   in `Shared/`, area auth in `_Imports.razor`.
4. **No behavior changes** unless a phase explicitly says so. Mechanical moves and
   dedup must be verifiable as behavior-preserving.

## New coding rules

These rules are enforced going forward (also recorded in
[CONTRIBUTING.md](../CONTRIBUTING.md)); the phases below retrofit the existing code:

- Discord: every feature lives in its own `<Feature>/` folder — no feature files at the
  project root.
- Web: non-routable components go in `Components/Shared/` (or their feature folder),
  never loose under `Pages/`; area-wide auth via the area's `_Imports.razor`.
- Web: STFC CRUD pages use the entity-page base classes (Phase 3); feature editors use
  the `FeatureEditorBase` setting helpers (Phase 3); nav entries come from the page
  registries (Phase 3).
- DI: `HoshiBot.Data` services are registered via one shared extension method (Phase 2).
- Jobs: per-guild Quartz jobs use the shared per-guild runner (Phase 4);
  first-detection-insert jobs carry `[DisallowConcurrentExecution]`.
- User-facing strings: once Phase 6 ships, all Discord-facing text comes from the
  localization catalog, never hardcoded.

## Verification (applies to every phase)

- `dotnet build && dotnet test && dotnet format --verify-no-changes`.
- Anything visual in `HoshiBot.Web`: screenshot + user sign-off before committing (see
  CLAUDE.md collaboration notes).
- Live-test affected Discord interactions in the test guild — interaction-timing bugs
  (double-ack, expired interactions) do not show up in build/tests.
- Phase 1 has its own dedicated verification steps (snapshot diff, history rewrite).

---

## Phase 0 — Repo hygiene (small, immediate) — DONE (2026-07-26)

- ~~Fix the `HoshiBot.StfcCatalogSync` → `HoshiBot.StfcSeedSync` naming in README's
  project table and `.gitignore`~~ — done alongside this plan's introduction.
- Add `.editorconfig` (C#/Razor style matching current `dotnet format` defaults, so it
  documents rather than changes the style) and `Directory.Build.props`
  (`net10.0`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`) so per-csproj
  duplication of these settings disappears. Consider `Directory.Packages.props`
  (central package management) — Serilog/Npgsql package versions are currently
  copy-pasted between `Host` and `Web`.
- Add GitHub Actions CI (`.github/workflows/ci.yml`): on push/PR run `dotnet build`,
  `dotnet test`, `dotnet format --verify-no-changes`. This makes the format rule and
  tests enforced instead of discipline-only.
- Delete the stale SQLite-era comments ("SQLite's EF Core provider can't translate …
  in ORDER BY") in 22 files — 17 `Components/Pages/Manage/Database/*.razor` + 5
  `Components/Pages/Manage/Stfc/*Pages/Index.razor` — and restore `Sortable="true"` on
  the affected `DateTimeOffset`/`ulong` columns; Postgres translates them fine.

## Phase 1 — Migration reset — DONE (2026-07-26)

Only one live database matters and it has the latest migration applied, so the entire
migration history can be collapsed into a single baseline.

1. Delete `src/HoshiBot.Data/Migrations/` (139 files, including the model snapshot).
2. Scaffold a fresh baseline: `cd src/HoshiBot.Data && dotnet ef migrations add
   InitialCreate`.
3. **Verify the model didn't drift**: diff the regenerated
   `HoshiBotDbContextModelSnapshot.cs` against the deleted one (`git diff`) — it must be
   model-identical (ordering aside). Any real difference means the old snapshot and
   model were out of sync; stop and investigate before proceeding.
4. Re-baseline every existing database **in place** (data is kept; no schema change
   executes). Via psql (local: `docker compose exec -T postgres psql -U hoshibot -d
   hoshibot`; testing VM: same command on the VM):

   ```sql
   TRUNCATE "__EFMigrationsHistory";
   INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
   VALUES ('<timestamp>_InitialCreate', '<EF Core version>');
   ```

   Use the exact migration ID from the generated file name and the EF Core version the
   old history rows carried.
5. Sanity check: run the migrator against each DB — it must report "Schema is up to
   date" and apply nothing.
6. A fresh, empty database still bootstraps correctly from the single `InitialCreate`
   (verify locally by creating a throwaway DB and running the migrator against it).

Note for the future: this reset is repeatable — but only while every deployed DB is at
the tip of history. Once real production guilds exist on separately-managed databases,
squashing requires coordinating the history rewrite across all of them.

## Phase 2 — Structure moves (mechanical, no behavior change) — DONE (2026-07-26)

### HoshiBot.Discord root cleanup

Move the feature files at the project root into feature folders (namespaces follow
folders; update usings):

- `AbsenceModule.cs` → `Absences/` (its four siblings already live there).
- New `Alerts/`: `AlertModule.cs`, `AlertService.cs`.
- New `TerritoryCapture/`: `TerritoryCaptureButtonModule.cs`,
  `TerritoryCaptureDigestService.cs` (the five TC Quartz jobs stay in `Scheduling/` for
  now — jobs are pooled there by convention).
- New `Alliances/`: `AllianceModule.cs`. New `Players/`: `PlayerModule.cs`,
  `StationHousingSystemAutocompleteProvider.cs` (goes with the feature that uses it —
  verify at move time).

Stay at root (genuinely cross-cutting): `EmbedBranding`, `EmbedBrandingOptions`,
`EphemeralReply`, `InteractionResponseExtensions`, `CommanderName`, `HoshiPersona`,
`PingModule`, `PendingModalInputService`, `BetaTesterService`, `ShieldLossVariant` (or
fold into its feature folder if it's single-feature — check usage).

### Web → Data service relocation

`src/HoshiBot.Web/Services/` hosts services that depend on nothing but
`HoshiBotDbContext` + `HttpClient` — per the architecture rule they belong in
`HoshiBot.Data`:

- The five importers: `StfcPlayerImportService`, `StfcAllianceImportService`,
  `StfcCatalogImportService`, `StfcServerStatusImportService`,
  `StfcTerritoryOwnershipImportService`.
- The three fetch/sync services: `StfcTerritoryOwnershipSyncService`,
  `TerritoryServiceSyncService`, `StfcSystemSyncService`.
- Collapse the two line-for-line clone hosted services
  (`TerritoryOwnershipAutoSyncService`, `TerritoryServiceAutoSyncService`) into one
  generic `PeriodicSyncService<TSync>` (interval + log-name parameters).

Optional follow-up (not required): converting the periodic syncs into Quartz jobs in
`Host` would stop tying ownership/services/system refresh to the *web* container being
up — decide when touching them next.

Also considered, dropped on inspection: unifying `tools/HoshiBot.StfcSeedSync` with the
importers. The tool turned out to be a file transformer (raw snapshots → embedded seed
JSON), not a DB importer — the "same issue and fix" cross-reference is about shared
parsing quirks, not duplicated code — so there is no second copy to remove.

### DI registration

Add `AddHoshiBotDataServices()` to `Data/ServiceCollectionExtensions.cs` registering the
shared Data services (`GuildFeatureService`, `GuildFeatureSettingsService`,
`GuildFeatureChannelService`, `GuildAllianceService`, `AiChatHealthService`,
`MemoryService`, `PlayerLinkService`, `MemberNoteService`, `GuildAccessService`, …) and
call it from both `Host/Program.cs` and `Web/Program.cs`. This fixes real drift (Web
currently omits `MemberNoteService`; Host omits `GuildAccessService` — verify each is
actually wanted in both roots at implementation time).

While in there: split `Data/ServiceCollectionExtensions.cs` (517 lines) — move the 12
`Seed*IfEmptyAsync` methods into `Data/Seeding/` (e.g. one static `Seeders` class or one
file per seeder group).

### Web placement fixes

- Move the seven shared non-routable components out of `Pages/` into
  `Components/Shared/`: `ScopeEditor`, `SettingsEditor`, `AlertChannelListEditor`
  (from `Pages/Manage/Guild/`), `FeatureEnableSwitch`, `ExtraPageLink`,
  `RoleTierEditor`, `AiHealthBanner` (from `Pages/Manage/Guild/Features/`). Per-feature
  `*Editor.razor` files stay co-located with their feature — they are feature-specific.
- Add `Pages/Manage/Bot/_Imports.razor` with
  `@attribute [Authorize(Policy = "GlobalAdmin")]` (mirroring `Manage/Stfc/` and
  `Manage/Database/`), then strip the four hand-rolled auth guards from
  `GlobalAdmins.razor`, `TrustedUsers.razor`, `StfcNewsSettingsEditor.razor`,
  `IncursionsSchedule.razor` (~28 duplicated lines each).
- Rename `Bot/StfcNewsSettingsEditor.razor` → `StfcNewsSettings.razor` (it's a routable
  page; the `…Editor` suffix is the convention for embedded editor components, and its
  three siblings don't carry it).

## Phase 3 — Web dedup — DONE (2026-07-26)

### STFC CRUD scaffold (~3,400 duplicated lines)

The `Manage/Stfc/*Pages/{Create,Edit,Delete,Index}.razor` files are verbatim copies
apart from entity/DbSet/key (compare `RegionPages/Edit.razor` vs `ServerPages/Edit.razor`
vs `TerritoryPages/Edit.razor`). Introduce:

- `EntityCreatePageBase<TEntity>` — form post, add, redirect.
- `EntityEditPageBase<TEntity, TKey>` — load + `NavigationManager.NotFound()`,
  `Attach/Modified` save, the `DbUpdateConcurrencyException` + `ExistsAsync` block
  (currently copied 12×).
- `EntityDeletePageBase<TEntity, TKey>` — load, confirm (`<DeleteConfirmation>`),
  delete, redirect.
- An `EntityIndexPage`/`ReadOnlyEntityIndex` shell for the list pages: "Create New"
  button + edit/delete `TemplateColumn` + `PoweredBy` footer (repeated 11×); the
  read-only variant makes the intentionally Index-only entities
  (`AllianceNameHistory`, `PlayerNameHistory`, `System`, `EventStatus`,
  `TerritoryService`) explicit.

Each concrete page then declares only: route, entity, form fields (`<FormField>`), and
grid columns. Also normalize the 12 `<p><em>Loading...</em></p>` copies via the
`LoadingPlaceholder` below.

### Feature-editor setting helpers (~105 call sites)

Every feature editor spells out
`Settings.GetSnowflakeAsync(GuildId, Feature, ResolvedAudience, GuildAllianceId, key)` /
`SetSnowflakeAsync(...)` per setting, each with its own `string? xInput` field and
one-line `SaveXAsync()` (e.g. `Absences/AbsencesEditor.razor`,
`Announcements/AnnouncementsEditor.razor`). `FeatureModuleContext` in
`IFeatureModule.cs` already proves the wrapper shape. Add to `FeatureEditorBase`:
`GetSnowflakeAsync(key)`, `SaveSnowflakeAsync(key, input)`, `GetTextAsync(key)`,
`SaveTextAsync(key, value)`, plus the secret variants — then sweep the editors.

### Shared ensure-role/channel helper

The try/catch around role/channel creation with the error string *"Could not create a
role on Discord — does the bot have Manage Roles permission in this server?"* is copied
14× across 10 files (`SettingsEditor`, `SetupWizard`, `Alliance/Settings`, and seven
feature editors), with three different field names for the same state. Add
`EnsureRoleOrErrorAsync`/`EnsureChannelOrErrorAsync` returning
`(ulong? Id, string? Error)` + one shared error-string constant.

### Nav & page registries

- `GuildAdminPages.cs`, `AllianceAdminPages.cs`, `MePages.cs` are three near-identical
  `record + static list + Href()` files (their own comments say "kept identical in
  shape"). Unify into one `AdminPage` record + group registry, and add registries for
  the Bot, STFC Catalog, and Database page groups.
- Drive `NavMenu.razor` (358 lines) from those registries: today the Bot/Catalog/
  Database groups hand-roll 41 `<NavLink>`s and repeat every href a second time in the
  `BotHrefs`/`CatalogHrefs`/`DatabaseHrefs` active-group sets (82 duplicated string
  literals; adding a page takes 2 edits). After: adding a page = one registry entry.
- `NavCard` wrapper for the overview-card markup (icon + title header, subtitle body)
  currently copied 5× (`Manage/Guild/Index`, `Manage/Guild/Alliance/Index`,
  `Me/Index`, `Manage/Index` ×2), and `FeatureCatalog.RelevantTo(audience)` for the
  alliance-relevant feature filter duplicated (with its explanatory comment) in both
  overview pages.

### Misc Web dedup

- `SnowflakeAllowListEditor` component replacing the `GlobalAdmins.razor` /
  `TrustedUsers.razor` near-clones (same guard/table/add-form/Load-Add-Delete against a
  one-column allow-list; reusable for future allow-lists).
- Extend `ImportForm` to cover multi-file/pre-upload-selector cases and adopt it in
  `PlayerPages/Import.razor` and `ServerPages/Import.razor` (both currently hand-roll
  the `importing`/`error`/`result` state machine `ImportForm` already implements).
- Extract `RegionServerPicker` — the cascading Region→Server `<Select>` pair with
  reload-on-region-change is duplicated in `PlayerPages/Import.razor` and three times in
  `Guild/ScopeEditor.razor`.
- `LoadingPlaceholder` component — 13 sites, 4 different wordings (`Loading...`,
  `Loading…`, `<em>Loading...</em>`, `Lade Notizen…`).
- Move the member-display-name lookup onto `DiscordGuildDataService` (or a small
  `MemberDirectory` value type): the `MemberName(ulong)` helper + preload is verbatim in
  `MemberInterviewsAdmin.razor`, `MemberNotesAdmin.razor`, `MemoryAdmin.razor`.
- Replace `MemberNotesAdmin.razor`'s raw `RenderTreeBuilder` lambdas with a small
  `NoteField` component.

## Phase 4 — Discord dedup — DONE (2026-07-26)

- **Tier role sync**: `Scheduling/RankRoleSyncJob.cs` and
  `Scheduling/OpsLevelRoleSyncJob.cs` are token-identical apart from the tier enum and
  one bucketing line. Replace with a generic `ExclusiveTierRoleSyncJob<TTier>` (member →
  `TTier?` selector + `RoleForTier` key function), two thin subclasses.
- **Per-guild job runner**: ~12 jobs repeat the same preamble
  (`GetEnabledGuildIdsAsync` → `foreach` → `IsEnabledAsync(guildId, feature,
  audience, null)` → optional per-guild try/catch + `LogWarning`) —
  `RankRoleSyncJob`, `OpsLevelRoleSyncJob`, `NicknameSyncJob`, `PlayerLinkSyncJob`,
  `MemberOnboardingSyncJob`, `AbsenceReportRefreshJob`, `MemoryConsolidationJob`,
  `AiChatIndexJob`, `MemberInterviewInviteJob`, `MemberInterviewExtractionJob`,
  `TerritoryCaptureRoleSyncJob`, `AnnouncementForwarderCatchUpJob`. Add
  `GuildFeatureJobRunner.ForEachEnabledGuildAsync(feature, audience, body)` (or a
  `PerGuildJobBase`); it also supplies the per-guild try/catch some jobs currently lack,
  so one failing guild can't abort the rest.
- **Notify-job scaffold**: `ServerStatusNotifyJob`, `InfiniteIncursionsNotifyJob`,
  `AllianceTournamentNotifyJob`, `StfcClientReleaseNotifyJob` share the diff-and-notify
  shape (diff `Notified*` vs observed → resolve guilds from `GuildServers` →
  `BuildBrandedAsync` + `SendPublicToEnabledAudiencesAsync` → write `Notified*` back →
  one `SaveChangesAsync`). Extract the scaffold; keep per-job diff/message logic.
  Preserve `[DisallowConcurrentExecution]` semantics where present.
- **`NotificationDispatcher.cs` internal dedup**: `SendToChannelsAsync` /
  `SendToChannelIdsAsync` duplicate the send + `Forbidden`/`NotFound` catch +
  skipped-channel log + admin-notify sequence; the two `SendDirectMessageAsync`
  overloads duplicate their bodies apart from `Components = [row]` vs `rows`.
- **Scope resolution helper**: the audience/alliance resolution from a component
  custom-id (`Enum.Parse<GuildAudience>` + `audience == Alliance ?
  await allianceService.GetPrimaryIdAsync(...) : null`) is inlined in
  `CommandBridgeButtonModule`, `AnnouncementButtonModule`,
  `AnonymousMessageModalModule`, `TicketModalModule` (+2 more). Move to
  `GuildAllianceService` or an interaction-context extension.

## Phase 5 — Big-file splits & careful genericization (lowest urgency)

- `Pages/Manage/Guild/PermissionCheck.razor` (921 lines): extract the two audit engines
  (`RunAudit`, `RunBotAccessCheckAsync`) + fix paths into a `PermissionAuditService`
  and split the markup into ~3 child components.
- Continue the `AiChatService` partial-class split (1,094 lines; `.Compose.cs` already
  started the pattern). Review `AiChatIndexService` (799), `TerritoryCaptureDigestService`
  (658), `PlayerLinkService` (528) for seams — split only where a clean seam exists.
- `GuildFeatureSettingsService` (`Data`): the Snowflake/Text method pairs repeat the
  same `FeatureScopeGuard.Validate` + 5-predicate `Where` + upsert logic (~90 lines).
  **Care required**: the two `Set*` methods carry *deliberately different* collision
  workarounds (both documented against real Postgres 23505 failures) — genericize only
  if both behaviors are preserved exactly, otherwise leave as-is.
- `HoshiBot.Domain` references the `Pgvector` package (an infra type leaking into the
  "pure" project). Known wart; fix only if a clean seam appears (e.g. mapping the
  vector column in `Data` configurations instead) — optional.

## Phase 6 — Localization of Discord-facing text

German is the current, temporary primary language for bot-facing text. Target: full
localization with English as the base language.

1. **Safeguard the current German first.** Extract every user-facing German string —
   embeds, button labels, select options, modal titles, error messages, job
   notifications — from `HoshiBot.Discord` *and* the Data-layer label/message helpers
   (`GuildFeatureService.FeatureLabel`/`AudienceLabel`/`DisabledMessage`) into a
   resource catalog (`.resx` per assembly, or a JSON/DB-backed string catalog) as the
   `de` locale **before any translation work**. The current German wording is the
   authoritative source and must not be lost; the extraction commit must be
   behavior-identical (same strings, now resolved through the catalog).
2. Translate the catalog to English; `en` becomes the neutral/base language (fallback
   for missing keys).
3. Add translations for the languages STFC itself supports (e.g. French, German,
   Italian, Spanish, Portuguese, Russian, Japanese, Korean — **confirm the exact set
   against the game before executing**).
4. Add a per-guild (or per-audience) language setting so each guild picks its bot
   language. Default existing guilds to `de` so nothing visibly changes at rollout.
5. Update [CONTRIBUTING.md](../CONTRIBUTING.md)'s language policy once this ships: new
   user-facing strings go into the catalog (all locales), never hardcoded.

## Explicit don't-touch list

Already well factored — re-working these is churn, not cleanup:

- `InteractionResponseExtensions` (ack-then-edit is fully centralized).
- `EmbedBranding` (`BuildBrandedAsync`/`EphemeralAsync`/`BrandedEditAsync`).
- `Scheduling/GuildRoster.cs`.
- `Host/Program.cs`'s `AddSimpleJob`/`AddCronJob` local helpers.
- `IFeatureModule`/`FeatureCatalog` metadata classes (26 small classes are legitimately
  distinct declarations, not boilerplate).
- `GeminiClient`/`OllamaClient` and the two embedding providers (superficially parallel,
  genuinely different SDK shapes).
- `ThreadRemovalRequest`/`ThreadCleanupJob` (deliberately kept infrastructure — see
  CONTRIBUTING "Design rules").
