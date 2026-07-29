# Localization plan (refactoring Phase 6)

Makes the bot's Discord-facing text multi-language. Today ~300–400 user-facing strings
are hardcoded (mostly German, some English) across 44 files; there is no catalog, no
language setting, and `Interaction.UserLocale` is never read. Launch languages:
**English + German**; the design accommodates all 9 STFC in-game languages
(en, fr, it, de, es, ru, pt, ja, ko — see "STFC in-game languages" in
[backlog.md](backlog.md)) without rework.

## Decided rules

- **Selectors on four levels**: guild, audience, alliance, user (per Discord user).
- **Defaults**: user → Discord `Interaction.UserLocale`; guild → the Discord guild's
  `preferred_locale`; audience & alliance → the guild's language. No seeding of
  existing guilds — the Discord-locale default applies everywhere; admins set the
  guild selector explicitly where wanted.
- **Rendering**: ephemeral interaction replies → user language; public posts → the
  owning scope's language (guild / audience / alliance); DMs → recipient's user
  language; posts into threads/forum posts **dedicated to a user** → that user's
  language. Exactly two dedicated-thread cases exist: RoE violation forum posts
  (addressee = `RoeViolationReport.ReportedByDiscordUserId`) and Ticket private
  threads (`Ticket.OpenedByDiscordUserId`).
- Public AiChat replies use the **channel's scope language** (not the author's).
- The transient "⏳ Processing..." placeholder is localized cheaply from
  `Interaction.UserLocale` only (no DB hit).
- Currently-English features (Scheduling notify jobs, StfcNews, PlayerModule,
  AllianceModule, AlertService's English half) get **authored German** during
  extraction so both locales end up complete.

## Architecture

### Catalog: typed accessors + per-locale embedded JSON (`HoshiBot.Domain`)

`src/HoshiBot.Domain/Localization/`:

- `Language.cs` — enum `En, De, Fr, It, Es, Ru, Pt, Ja, Ko` + static `Languages`
  helper: `Parse`/`ToCode` (lowercase ISO-639-1 — the DB representation),
  `FromDiscordLocale` (prefix match: `en-US`→En, `pt-BR`→Pt, `es-419`→Es; unknown →
  null), `ToCulture` (en-US, de-DE, …), `EnglishName`, and
  `Enabled = { En, De }` — the launch gate; selectors and the completeness test
  iterate this. Adding a locale = one JSON file + one `Enabled` entry.
- `MessageCatalog.cs` — lazy embedded-JSON loader (per-locale
  `FrozenDictionary<string,string>`); `Format(lang, key, params (name, value)[])`
  with `{name}` / `{name:format}` substitution (`IFormattable` formatted via
  `ToCulture(lang)`); fallback chain lang → en → raw key (never throws — the test
  suite is the guard). `FormatCount(lang, keyBase, count, …)` selects
  `key.one/.few/.many/.other` via `PluralRules.cs` (CLDR-lite: en/de/es/it/pt/fr
  one|other, ru one|few|many, ja/ko other-only).
- `Msg.*.cs` — static partial `Msg` accessor classes, one file per feature
  (`Msg.Tickets.cs`, `Msg.Roe.cs`, …): one typed method per message, so call sites
  are compile-checked; translators only ever touch JSON.
- `Locales/de.json`, `Locales/en.json` — flat key → template, `<EmbeddedResource>`
  in the csproj (mirrors `HoshiBot.Data`'s seed-JSON pattern).

Rejected alternatives: .resx (typed accessors are `CurrentUICulture`-ambient — wrong
for a concurrent multi-language bot; positional placeholders; no parity guarantees
without the same custom tests), per-locale C# classes (translators would edit C#).

### Storage (one additive migration `AddLanguageSettings`)

| Level | Column/table | null means |
|---|---|---|
| User explicit | `DiscordUser.Language` (text 10, null) | automatic (Discord locale) |
| User synced fact | `DiscordUser.DiscordLocale` (text 10, null) | last-seen `Interaction.UserLocale`, opportunistically recorded — lets DMs/jobs know a user's language without an interaction |
| Guild synced fact | `DiscordGuild.PreferredLocale` (text 10, null) | synced by `GuildSyncHandler` alongside Name/IconHash |
| Guild explicit | `GuildSettings.Language` (text 10, null) | derive from `PreferredLocale` |
| Alliance | `GuildAlliance.Language` (text 10, null) | inherit guild (sits next to `TimeZoneId` — same feature-agnostic-attribute precedent) |
| Audience | new `GuildAudienceLanguage` table, PK (GuildId, Audience) | row absent = inherit guild. Covers Server/VeilGroup/Community; the Alliance audience is per-alliance; the Guild pseudo-audience is `GuildSettings.Language` |

The dead `DiscordGuild.Locale` column (default "de", never synced or read) is
**dropped** — its stale values would masquerade as synced facts. Its Database
debug-grid column goes too. `GuildFeatureSettingText` is deliberately not reused:
feature-scoped (wrong altitude) and has no user dimension.

### Resolution

- `LanguagePolicy` (Domain, pure, fully unit-tested): `ForUser(explicit,
  interactionLocale, storedLocale, scopeFallback)`, `ForGuild(explicit,
  preferredLocale)` (→ En terminal fallback), `ForAudience`/`ForAlliance(explicit,
  guildLanguage)`.
- `LanguageResolver` (Data, scoped) + singleton `LanguageCache`
  (ConcurrentDictionary, 5-minute TTL — Web writes happen in another process, so TTL
  rather than invalidation; matches `DiscordGuildDataService`'s cache precedent):
  `ForGuildAsync(guildId)`, `ForAudienceAsync(guildId, audience)`,
  `ForAllianceAsync(guildAllianceId)`, `ForUserAsync(userId, interactionUserLocale =
  null, scopeGuildId = null)`, `RecordUserLocaleAsync(userId, locale)` (change-only
  upsert, invalidates the cache entry).
- `src/HoshiBot.Host/UserLocaleSyncHandler.cs` — interaction-create hook recording
  `Interaction.UserLocale` (fire-and-forget, skips unchanged values via the cache).
- Multi-alliance edge: `GuildAlertChannel` rows are audience-tagged but not
  alliance-tagged, so Alliance-audience alert rows resolve to guild language.

### Threading language through the funnels (minimal churn)

- `EmbedBranding`, `EphemeralReply.Of`, `SendDelayedEmbedAsync`: **unchanged** —
  callers localize content before/inside them; branding chrome is language-neutral.
- Services resolve language at the top of each operation (they hold db + ids) and
  pass `Language` into their private string builders. One operation may resolve two
  languages (ephemeral user-lang + public scope-lang) — intended.
- `NotificationDispatcher` gains factory overloads (`Func<Language,string> content`,
  `Func<Language,EmbedProperties?> embed`) resolving per alert-channel row's
  audience and memoizing per distinct language; the string overloads remain as
  delegations. `SendToChannelIdsAsync` takes an explicit `Language`;
  `SendDirectMessageAsync` keeps its signature (callers resolve the recipient's
  language first — all six DM sites hold guildId). The dispatcher's internal
  admin/log strings and the free-text `context`/`hint` parameters become catalog
  keys rendered in guild language.
- `GuildFeatureService.FeatureLabel/AudienceLabel/DisabledMessage` gain a `Language`
  parameter (parameterless overloads default to En for the Web admin).
  `CommandBridgeCatalog.LabelDe` → `LabelKey` (a catalog key rendered per language).
- **Web request language**: when the Web UI renders catalog content, the language is
  resolved as: the signed-in user's explicit `DiscordUser.Language` → the browser's
  `Accept-Language` request header (first supported match) → En. Applies equally to
  anonymous pages (no login → straight to the header). This governs Web *rendering*
  only — it never writes anything and is independent of the Discord-side resolver
  chains above.
- `CommanderName.Greeting/Address` (a German-grammar salutation split used at 16
  sites) is dissolved: extracted messages become full-sentence templates including
  the salutation; `CommanderName.Of` (tag stripping) stays.
- Dates: the two hardcoded `de-DE` weekday sites
  (`TerritoryCaptureDigestService.cs`, `AiChatService.Context.cs`) switch to
  `Languages.ToCulture(lang)`; Absence/StfcNews modal date placeholders become
  per-language (de keeps `dd.MM.yyyy`, others show ISO `yyyy-MM-dd`); parsing tries
  the resolved culture's short date, then `dd.MM.yyyy`, then `yyyy-MM-dd`. Discord
  `<t:…>` timestamps stay (already per-viewer).
- LLM prompts: the "Antworte auf Deutsch" instruction becomes an answer-language
  instruction from the resolved channel-scope language; HoshiPersona's four
  user-visible busy replies go through the catalog. Deferred: knowledge-base
  translation, per-language embeddings, `AnnouncementTranslator` (has its own
  `TargetLanguage` mechanism). `MemberInterviewService` already mirrors the member's
  language — unchanged.

### Web selectors

Shared `Components/Shared/LanguagePicker.razor`: options from `Languages.Enabled` +
a first "Default (inherited: {X})" entry (parent computes the inherited label);
empty value saves null.

1. **Guild** — `Shared/SettingsEditor.razor`: new "Regional" section (below Roles),
   BootstrapBlazor Select card, autosave; inherited label from
   `DiscordGuild.PreferredLocale`.
2. **Alliance** — `Manage/Guild/Alliance/Settings.razor`: Language card in the
   existing "Regional" section next to Timezone.
3. **Audience** — `Manage/Guild/Audience.razor`: new "Language per audience" section
   for the enabled non-Alliance audiences (upserts `GuildAudienceLanguage`); the
   Alliance row links to Alliance Settings.
4. **User** — `Pages/Me/Index.razor`: "My Language" section with a **plain HTML
   select** (MeLayout has no BootstrapBlazorRoot); saves `DiscordUser.Language`;
   default option "Automatic (Discord: {…})".

### Slash-command localization (last)

NetCord's `JsonLocalizationsProvider` via
`ApplicationCommandServiceConfiguration.LocalizationsProvider` in Host `Program.cs`,
with `Localizations/de.json`. Canonical names/descriptions become English (normalize
`hoshi-say`'s German parameter names; verify nothing reads options by German name);
command **names stay English** in all locales — only descriptions are localized.

## Sub-phases (each leaves build/tests/format green; output unchanged until 6e)

- **6a — Domain foundations**: `Language`/`Languages`, `MessageCatalog` +
  `PluralRules`, `LanguagePolicy`, near-empty de/en.json + EmbeddedResource. Tests
  in `tests/HoshiBot.Domain.Tests`: `LanguagesTests` (Discord-locale mapping),
  `LanguagePolicyTests` (fallback-chain matrices), `MessageCatalogTests` (key parity
  across `Enabled` locales, placeholder parity via regex, plural selection,
  formatting, fallback).
- **6b — Storage + resolver**: entity changes + `AddLanguageSettings` migration
  (incl. dropping `DiscordGuild.Locale` and its debug-grid column);
  `GuildSyncHandler` syncs `PreferredLocale`; `LanguageResolver` + `LanguageCache` +
  DI in both roots; `UserLocaleSyncHandler`.
- **6c — Web selectors**: `LanguagePicker` + the four placements. Visual → screenshot
  sign-off before commit.
- **6d — German catalog extraction** — **DONE 2026-07-28** (commits `3e04666..e9cff92`,
  ~280 catalog keys across 17 feature prefixes): all user-facing strings in
  HoshiBot.Discord and the Data-layer labels moved into `Msg.*` + de/en.json; call
  sites pass a pinned `Lang = Language.De` const → German output byte-identical
  (verified programmatically per commit); full-sentence salutation templates replace
  the `CommanderName.Greeting` concatenations (helpers still exist, deleted in 6e);
  currently-English features (AlertService's English half, `/absence`, the
  admin/notify jobs, Player/Alliance modules, StfcNews, warning jobs) got authored
  German and now render German. `CommandBridgeCatalog.LabelDe` → `LabelKey`;
  `GuildFeatureService.FeatureLabel/AudienceLabel/DisabledMessage/EnsureEnabledAsync`
  take a `Language`; new scoped `WebRequestLanguage` (user choice → Accept-Language →
  En) renders catalog content in the Web admin. Still hardcoded, on purpose:
  AiChat/HoshiPersona strings and all LLM prompts (6e), slash-command metadata
  incl. hoshi-say's German parameter names (6g), `AnnouncementTranslator` (own
  mechanism). The TC weekly digest title keeps its ambient-culture month names
  (production = invariant/English today) — its proper per-language form lands in 6e.
- **6e — Rendering switch** — **DONE 2026-07-29** (commits `388bcc3..5ff533b`): every
  pinned `Language.De` in HoshiBot.Discord replaced with resolved languages —
  ephemeral/modals → the acting user's `ForUserAsync` (with the interaction locale in
  modules); public posts → the owning scope (`ForAlliance/ForAudience/ForGuildAsync`;
  GuildAlertChannel fan-outs via the dispatcher's new `Func<Language,…>` factory
  overloads, memoized per language, Alliance rows → guild language); RoE forum
  posts/Ticket threads and all DMs → the addressee's `ForUserAsync`; admin
  notifications → guild language. The "⏳ Processing…" ack localizes synchronously
  from `Interaction.UserLocale` (U2). AiChat replies carry an "Answer in {X}."
  instruction from the channel's scope language (U3) and its date/weekday context
  renders per language; HoshiPersona's busy/cannot-answer replies moved to the
  catalog. Absence/StfcNews date input parses language-aware (`DateInput`, tested:
  culture short date → `dd.MM.yyyy` → ISO) and the TC digest dates render per
  language. `CommanderName.Address` deleted; `Greeting` survives solely for
  prefixing AiChat's dynamic LLM text ("Commander" is language-neutral in-game
  address).
- **6f — Defaults live** — **DONE 2026-07-29**: no seeding; the test guild (en-US
  `preferred_locale`, no selector) verified live — AiChat and public posts render
  English, per the defaults. Lost Falcons' `preferred_locale` is `de`, so production
  stays German with no action; set its guild selector only if they want German pinned
  regardless of a future Discord-locale change. CONTRIBUTING's language policy
  rewritten: catalog-only strings, resolved languages, German-is-temporary wording
  removed.
- **6g — Slash-command localization** — **DONE 2026-07-29** (commit `3626c0f`):
  canonical command metadata is English everywhere (hoshi-say's `auftrag`/`mitglied`
  → `task`/`member`); German lives in
  `src/HoshiBot.Host/Localizations/de.json` served by NetCord's
  `JsonLocalizationsProvider` (descriptions, option-name/description localizations,
  enum choice names — command names stay English in every locale, with one
  documented exception: the "Create preview" message command's name is localized,
  a context-menu entry's name being its only visible text). Along the way this
  fixed a pre-existing bug: the message command was never registered at all (the
  default hosting service only collects slash modules) — Host now registers two
  narrowed application-command services; expect "13 application command(s)
  registered" after deploy.

### Add-a-locale recipe (per additional language)

1. `src/HoshiBot.Domain/Localization/Locales/xx.json` — translate every key
   (copy `en.json` as the template).
2. Add the language to `Languages.Enabled` in
   `src/HoshiBot.Domain/Localization/Language.cs` — the enum member, code, culture
   and names already exist for all 9 STFC languages. The `MessageCatalogTests`
   parity suite then enforces key/placeholder completeness for the new locale, and
   every selector (guild/audience/alliance/user) offers it automatically.
3. `src/HoshiBot.Host/Localizations/xx.json` — slash-command descriptions (and
   option localizations) for the new locale; silently optional per key, but do the
   full file.
4. Check the special-cased formats: `DateInput.DateFormat` and the TC digest's
   `FormatLongDate` special-case German (`dd.MM.yyyy` / `d. MMMM yyyy`) and default
   everything else to ISO/English patterns — add a branch if the new language needs
   its own convention.
5. `src/HoshiBot.Domain/Localization/Locales/Web/xx.json` — the Web admin UI's
   catalog file (see Phase 7); same parity enforcement as the bot pair.

## Phase 7 — Web UI localization (EN + DE)

Localize the Web admin UI (~1,150 distinct hardcoded English strings across 187
razor components) with the same catalog infrastructure, rendered per request via
`WebRequestLanguage` (explicit `DiscordUser.Language` → `Accept-Language` → En).

**Scope** — in: the /manage Guild area (feature editors, SetupWizard,
PermissionCheck + PermissionAuditService, Alliance/Audience pages, Settings),
`Components/Shared`, layout/nav/breadcrumbs, /me, landing-page marketing,
Error/NotFound, the page registries + `IFeatureModule` titles/descriptions,
in-scope PageTitles. Out (stays English): `Manage/Database`, `Manage/Bot` and
`Manage/Stfc` (all GlobalAdmin-only operator areas), the legal footer disclaimer,
BootstrapBlazor built-in text (ships no `de` locale) and QuickGrid's paginator
(no localization hook) — the last two live in docs/backlog.md.

### Phase 7 design

- Second embedded locale pair `Locales/Web/{en,de}.json`, all keys `Web.`-prefixed;
  `MessageCatalog.Load` merges both resources per locale. Tests enforce bot/web key
  disjointness and the `Web.` prefix discipline on top of the existing parity suite.
- Typed `Msg.Web*` accessors per area, plus enum/slug-driven helpers:
  `Msg.WebFeature.Title/Description(lang, feature)`, `Msg.WebAudience`, and
  `Msg.WebEditor.Label/Usage/CardTitle(lang, feature, settingKey)` keyed by the
  existing `*SettingKeys` constants.
- Language delivery: each layout resolves `WebRequestLanguage` once in
  `OnInitializedAsync` and wraps its markup in `<CascadingValue Value="lang"
  IsFixed="true">`, rendering only after resolution (prerender is off);
  components consume `[CascadingParameter] Language Lang`. `AdminPage` carries
  keys + `Label(Language)`; excluded registries keep literal English in the key
  slot (raw-key fallback renders it verbatim). `IFeatureModule.Title/Description`
  are default interface methods over `Msg.WebFeature`.
- **Rule: never place localized strings into shared `IMemoryCache` entries** —
  cached values cross circuits/users; localize render-side instead.
- A `/me` language change forces a full reload (`forceLoad: true`) — the new
  circuit resolves the new language.
- Plural sites use `FormatCount`; admin-grid ISO timestamps and the
  TerritoryCapture time-input round-trip stay invariant; `<html lang>` derives
  from `Accept-Language` only (static SSR, no DB).

**Batches**: 0 foundation (loader/tests/reshapes/cascades, ~80 keys) → 1 shared +
nav chrome → 2 guild pages + audit → 3 feature editors A–I → 4 feature editors
M–T + extra pages → 5 alliance + /me → 6 landing + PageTitles → 7 docs. Each
batch lands green (build 0 warnings, tests incl. parity, format) with German
authored in the same commit. Known German-in-English-UI bugs ("⚠ Unbekannt",
"Ganze Kategorie", TerritoryCapture's German usage text) are fixed as their
batches touch them.

## Verification

- Per sub-phase: build (0 warnings), tests, format, CI green, deploy to the testing
  VM.
- 6b: migrator applies `AddLanguageSettings`; `GuildSyncHandler` populates
  `PreferredLocale` (visible in the Database debug grid); interactions populate
  `DiscordUser.DiscordLocale`.
- 6d: spot-diff rendered messages before/after extraction commits in the test guild
  (must be byte-identical); parity test green.
- 6e: live per-feature checks in the test guild — ephemeral reply follows the acting
  user's language; public post follows the scope selector; a RoE forum post and a
  Ticket thread follow the dedicated user; a shield-reminder DM follows the
  recipient.
