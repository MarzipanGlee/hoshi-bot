# Backend review checklist

Every admin page and every setting it shows, to tick off while reviewing the backend.

Generated from the code, not written by hand: the feature list comes from `GuildFeature`, the
cards from what each editor actually renders, and the labels from the English catalog. If a card
is missing here it is missing from the page too — worth reporting either way.

Scope column: which audiences a feature can be configured for (`Alliance`, `Server`, `Community`,
`VeilGroup`, `Guild` = guild-wide). A multi-audience feature needs checking once per audience the
guild actually serves.

**Legend** — `[ ]` not looked at · `[x]` checked, fine · add `⚠` and a note for anything wrong.

Every route in the app is covered by a line here, except the `/create`, `/edit` and `/delete` forms
under STFC data — one of each is enough, and they are noted in that section rather than listed.

---

## 1. Dashboard — `/manage`

The landing page after login, and the only route with no guild in it.

- [x] Welcome header and the "N guilds still need setup" banner
- [x] **Your Guilds** cards — audience badges, "needs setup" flag, Features / Settings / Setup Wizard buttons
- [x] Guilds the bot is not in yet — the **+ Install bot** button
- [x] **Foreign servers** section — only visible with support mode on
- [x] Bot / STFC Catalog / Database cards
- [x] Logged-out state — `/manage` redirects to the landing page, which is where the login lives
  (there is no prompt on `/manage` itself; the item said otherwise and was wrong)
- [x] Support toggle and Bot / STFC Catalog / Database cards and nav menu not visible for non global admin users
- [x] A guild you may not administer redirects to `/manage` (the dashboard), not to the landing page

## 2. Guild pages

- [x] **Overview** — `/manage/guild/{id}`
- [x] **Audience** — `/manage/guild/{id}/audience`
- [x] **Alliances** — `/manage/guild/{id}/alliances`
- [ ] **Features (guild-wide)** — `/manage/guild/{id}/features`
- [ ] **Setup Wizard** — `/manage/guild/{id}/setup-wizard`
- [ ] **Permission Check** — `/manage/guild/{id}/permission-check`

### Guild settings — `/manage/guild/{id}/settings`

- [x] Log
- [x] Admin
- [x] User Log
- [x] Crews — marked "not implemented yet"; nothing reads it. Moving it to the alliance belongs with
  the crew feature itself, so it stays here until then.
- [x] Language

## 3. Alliance pages

Per linked alliance. A coalition guild must check each one — several settings are per-alliance.

- [ ] **Overview** — `/manage/guild/{id}/alliance/{allianceId}`
- [ ] **Features** — `/manage/guild/{id}/alliance/{allianceId}/features`
- [ ] **No-id fallback** — `/manage/guild/{id}/alliance` and `/manage/guild/{id}/alliance/settings` land on the alliance you last used (there is no dropdown any more; the sidebar lists them)

### Alliance settings — `/manage/guild/{id}/alliance/{allianceId}/settings`

Role cards appear only while a feature that reads them is enabled (`SharedSettingUsage`), so a
card missing here may be correct — check with the relevant feature on.

- [ ] Default Channel Category
- [ ] Alliance Boarding
- [ ] Reminders (Allies)
- [ ] Rules (DE)
- [ ] Rules (EN)
- [ ] User Notifications
- [ ] Senior Staff Jobs
- [ ] Member
- [ ] Diplomat
- [ ] Senior Staff
- [ ] Alerts
- [ ] Notifications
- [ ] Timezone
- [ ] Language

## 4. Audience pages

Once per audience the guild serves (Server, Community, Veil Group).

- [ ] **Features** — `/manage/guild/{id}/{audience}/features`

### Audience settings — `/manage/guild/{id}/{audience}/settings`

- [ ] Nothing to link
- [ ] Default Channel Category
- [ ] Senior Staff Role
- [ ] Language

## 5. Features

35 features. Each is reachable at `.../features/{slug}` within its scope.

| ✔ | Feature | Scope | Slug | Settings |
|---|---|---|---|---|
| [ ] | AI Chat | Alliance+Server+VeilGroup+Community | `ai-chat` | 10 |
| [ ] | AI Provider | Guild | `ai-backend` | 7 |
| [ ] | Absences | Alliance | `absences` | 3 |
| [ ] | Alliance Tag Roles | Guild | `alliance-tag-roles` | 8 |
| [ ] | Alliance Tournament Announcements | Alliance+Server+VeilGroup | `alliance-tournament` | 1 |
| [ ] | Announcement Forwarder | Alliance+Server+VeilGroup+Community | `announcement-forwarder` | 4 |
| [ ] | Announcements | Alliance+Server+VeilGroup+Community | `announcements` | 6 |
| [ ] | Anonymous Messages | Alliance+Server+VeilGroup+Community | `anonymous-messaging` | 1 |
| [ ] | Bot Support | Alliance | `bot-support` | 1 |
| [ ] | Channel Guide | Alliance | `channel-guide` | 1 |
| [ ] | Client Release Announcements | Alliance+Server+VeilGroup | `client-release` | 7 |
| [ ] | Command Bridge | Alliance | `command-bridge` | 2 |
| [ ] | Conditional Roles | Guild | `conditional-roles` | 5 |
| [ ] | Diplomacy | Alliance | `diplomacy` | 2 |
| [ ] | Hoshi Say | Guild | `hoshi-say` | 1 |
| [ ] | Infinite Incursions Announcements | Alliance+Server+VeilGroup | `infinite-incursions` | 1 |
| [ ] | Member Lore | Alliance | `member-lore` | 7 |
| [ ] | Member Onboarding | Community | `member-onboarding` | 3 |
| [ ] | Nickname Sync | Guild | `nickname-sync` | 7 |
| [ ] | Notification Opt-In | Alliance | `notification-opt-in` | 1 |
| [ ] | Ops Level Roles | Guild | `ops-level-roles` | 1 |
| [ ] | Player Assignment | Guild | `player-link` | 4 |
| [ ] | Raid Alerts | Alliance | `raid-alerts` | 7 |
| [ ] | Rank Roles | Guild | `rank-roles` | 2 |
| [ ] | Read Confirmation | Alliance+Server+VeilGroup+Community | `read-receipts` | 1 |
| [ ] | RoE Violation Reports | Alliance | `roe-violation-reports` | 3 |
| [ ] | STFC News | Alliance | `stfc-news` | 3 |
| [ ] | Server Status | Alliance+Server+VeilGroup | `server-status` | 1 |
| [ ] | Server Tag Roles | Guild | `server-tag-roles` | 4 |
| [ ] | Services Role Sync | Alliance | `services-role-sync` | 1 |
| [ ] | Shield Reminders | Alliance | `shield-reminders` | 3 |
| [ ] | Territory Capture Reminders | Alliance | `territory-capture` | 8 |
| [ ] | Territory Capture Service Reminders | Alliance | `territory-capture-service-reminders` | 3 |
| [ ] | Territory Capture Sign-Off | Alliance | `territory-capture-sign-off` | — switch only |
| [ ] | Tickets | Alliance+Server+VeilGroup+Community | `tickets` | 1 |

---

### AI Chat

`ai-chat` · Alliance+Server+VeilGroup+Community

- [ ] Enable switch, description and any "requires" badges
- [ ] AI Chat health
- [ ] Listen channels
- [ ] Knowledge – preferred
- [ ] Knowledge – normal
- [ ] Knowledge – last resort
- [ ] System prompt (optional)
- [ ] Response streaming
- [ ] Memory (experimental)
- [ ] Open memories
- [ ] Search language
- [ ] Extra page: `memories`
- [ ] Extra page: `health`

### AI Provider

`ai-backend` · Guild

- [x] Enable switch, description and any "requires" badges
- [x] AI provider
- [x] Gemini API key
- [x] Model (optional)
- [x] Gate model (optional)
- [x] Router model (optional)
- [x] Embeddings (optional)
- [x] Image embeddings (optional)

### Absences

`absences` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Absences Report
- [ ] Absences Report (Staff)
- [ ] Notification Role

### Alliance Tag Roles

`alliance-tag-roles` · Guild

- [x] Enable switch, description and any "requires" badges
- [x] How it works
- [x] Create Missing Roles
- [x] Plain Letters
- [x] Lower Case
- [x] Prefix and Suffix
- [x] Foreign-Alliance Role
- [x] No-Alliance Role
- [x] Roles in use

### Alliance Tournament Announcements

`alliance-tournament` · Alliance+Server+VeilGroup

- [ ] Enable switch, description and any "requires" badges
- [ ] Alert Channels

### Announcement Forwarder

`announcement-forwarder` · Alliance+Server+VeilGroup+Community

- [ ] Enable switch, description and any "requires" badges
- [ ] Source channels
- [ ] Destination channel
- [ ] Target language (optional)
- [ ] Catch-up window (optional)

### Announcements

`announcements` · Alliance+Server+VeilGroup+Community

- [ ] Enable switch, description and any "requires" badges
- [ ] Announcements
- [ ] Announcements Draft
- [ ] Test channel (optional)
- [ ] Warnings Role
- [ ] Senior Staff Role
- [ ] Notification Role

### Anonymous Messages

`anonymous-messaging` · Alliance+Server+VeilGroup+Community

- [ ] Enable switch, description and any "requires" badges
- [ ] Anonymous Messages

### Bot Support

`bot-support` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Support channel

### Channel Guide

`channel-guide` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Guide message

### Client Release Announcements

`client-release` · Alliance+Server+VeilGroup

- [ ] Enable switch, description and any "requires" badges
- [ ] Announcement Channels
- [ ] Platform Roles (guild-wide)
- [ ] Windows
- [ ] macOS
- [ ] Android
- [ ] iOS
- [ ] Notification Opt-In (link only)

### Command Bridge

`command-bridge` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Channel
- [ ] false

### Conditional Roles

`conditional-roles` · Guild

- [ ] Enable switch, description and any "requires" badges
- [ ] How it works
- [ ] Rules
- [ ] Edit rules
- [ ] Reusable Conditions
- [ ] Edit conditions
- [ ] Extra page: `rules`
- [ ] Extra page: `conditions`

### Diplomacy

`diplomacy` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Diplomacy
- [ ] Diplomat Role

### Hoshi Say

`hoshi-say` · Guild

- [x] Enable switch, description and any "requires" badges
- [x] Allowed role

### Infinite Incursions Announcements

`infinite-incursions` · Alliance+Server+VeilGroup

- [ ] Enable switch, description and any "requires" badges
- [ ] Alert Channels

### Member Lore

`member-lore` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Member role
- [ ] Completed role
- [ ] Interviews per day
- [ ] Campaign
- [ ] View interviews
- [ ] Notes & review
- [ ] Open notes & review
- [ ] Extra page: `notes`
- [ ] Extra page: `interviews`

### Member Onboarding

`member-onboarding` · Community

- [ ] Enable switch, description and any "requires" badges
- [ ] How it works
- [ ] DMs per day
- [ ] Campaign

### Nickname Sync

`nickname-sync` · Guild

- [x] Enable switch, description and any "requires" badges
- [x] How it works
- [x] Alliance tag
- [x] Server tag — renders `[EU-164]` now
- [x] No-Alliance Tag (new)
- [x] Member Name Suffix
- [x] Excluded roles — the add picker can create `No-Nickname-Sync`

### Notification Opt-In

`notification-opt-in` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Alert Role

### Ops Level Roles

`ops-level-roles` · Guild

- [ ] Enable switch, description and any "requires" badges
- [ ] Role tiers

### Player Assignment

`player-link` · Guild

- [ ] Enable switch, description and any "requires" badges
- [ ] How it works
- [ ] Open player assignments
- [ ] Status
- [ ] Not-Linked Role
- [ ] Extra page: `assignments`

### Raid Alerts

`raid-alerts` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Weekly Raid Report
- [ ] Report Time
- [ ] Alert Role
- [ ] Senior Staff Role
- [ ] Notification Role
- [ ] Notification Opt-In (link only)
- [ ] Alert Channels

### Rank Roles

`rank-roles` · Guild

- [ ] Enable switch, description and any "requires" badges
- [ ] No-Rank Role
- [ ] Role tiers

### Read Confirmation

`read-receipts` · Alliance+Server+VeilGroup+Community

- [ ] Enable switch, description and any "requires" badges
- [ ] Post kinds

### RoE Violation Reports

`roe-violation-reports` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] RoE Violations
- [ ] Senior Staff Role
- [ ] Diplomat Role

### STFC News

`stfc-news` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Admin
- [ ] Where Hoshi posts things that need a decision from you, such as STFC news date confirmations.
- [ ] Senior Staff Role

### Server Status

`server-status` · Alliance+Server+VeilGroup

- [ ] Enable switch, description and any "requires" badges
- [ ] Alert Channels

### Server Tag Roles

`server-tag-roles` · Guild

- [ ] Enable switch, description and any "requires" badges
- [ ] How it works
- [ ] Foreign-Server Role
- [ ] Server Roles
- [ ] false

### Services Role Sync

`services-role-sync` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Services Role

### Shield Reminders

`shield-reminders` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Alert Role
- [ ] Notification Opt-In (link only)
- [ ] Alert Channels

### Territory Capture Reminders

`territory-capture` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Digest Channel
- [ ] Slot 1
- [ ] Slot 2
- [ ] Slot 3
- [ ] Slot 4
- [ ] Slot 5
- [ ] Digest Instructions
- [ ] Notification Role

### Territory Capture Service Reminders

`territory-capture-service-reminders` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Services Channel
- [ ] Services Role
- [ ] Service Selection
- [ ] Extra page: `service-selection`

### Territory Capture Sign-Off

`territory-capture-sign-off` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] No settings of its own — the switch is the whole configuration

### Tickets

`tickets` · Alliance+Server+VeilGroup+Community

- [ ] Enable switch, description and any "requires" badges
- [ ] Tickets

## 6. Bot administration

- [ ] **Global Admins** — `/manage/bot/global-admins`
- [ ] **Trusted Users** — `/manage/bot/trusted-users`
- [ ] **STFC News settings** — `/manage/bot/stfc-news-settings`
- [ ] **Incursions schedule** — `/manage/bot/incursions-schedule`

## 7. STFC data

Shared game data, not per guild. List/create/edit/delete per area — checking the list and one
edit is usually enough. The `/import` pages are separate — they take a paste or upload rather than
one row, so they are listed on their own.

- [ ] `/manage/stfc/alliance-diplomacy`
- [ ] `/manage/stfc/alliance-invites`
- [ ] `/manage/stfc/alliance-name-history`
- [ ] `/manage/stfc/alliances`
- [ ] `/manage/stfc/event-status`
- [ ] `/manage/stfc/player-name-history`
- [ ] `/manage/stfc/players`
- [ ] `/manage/stfc/regions`
- [ ] `/manage/stfc/server-invites`
- [ ] `/manage/stfc/server-status`
- [ ] `/manage/stfc/servers`
- [ ] `/manage/stfc/systems`
- [ ] `/manage/stfc/territories`
- [ ] `/manage/stfc/territory-neighbours`
- [ ] `/manage/stfc/territory-ownership`
- [ ] `/manage/stfc/territory-services`
- [ ] `/manage/stfc/veil-group-invites`
- [ ] `/manage/stfc/veil-groups`

### Imports

Separate from the list/create/edit pages above: these take a paste or a file rather than one row.

- [ ] `/manage/stfc/alliances/import`
- [ ] `/manage/stfc/players/import`
- [ ] `/manage/stfc/servers/import`
- [ ] `/manage/stfc/server-status/import`
- [ ] `/manage/stfc/territory-ownership/import`

## 8. Database browser

Read-only tables under `/manage/database/*`. Worth a pass for empty states and column labels.

- [ ] `/manage/database/absences`
- [ ] `/manage/database/alert-notifications`
- [ ] `/manage/database/alerts`
- [ ] `/manage/database/announcements`
- [ ] `/manage/database/client-releases`
- [ ] `/manage/database/discord-guilds`
- [ ] `/manage/database/discord-users`
- [ ] `/manage/database/guild-members`
- [ ] `/manage/database/pending-modal-inputs`
- [ ] `/manage/database/read-receipts`
- [ ] `/manage/database/readable-posts`
- [ ] `/manage/database/roe-violation-reports`
- [ ] `/manage/database/shield-reminder-notifications`
- [ ] `/manage/database/shield-reminders`
- [ ] `/manage/database/stfc-event-date-confirmations`
- [ ] `/manage/database/stfc-news-post-guild-messages`
- [ ] `/manage/database/stfc-news-posts`
- [ ] `/manage/database/thread-removal-requests`
- [ ] `/manage/database/tickets`
- [ ] `/manage/database/user-players`

## 9. Member-facing

- [ ] **My profile** — `/me`
- [ ] **My lore** — `/me/lore`
- [ ] **Home / login** — `/`
- [ ] **Not found** — `/not-found`
- [ ] **Error** — `/Error`

---

## Notes

Anything found goes here, or straight into [backlog.md](backlog.md) if it is a deferred idea
rather than a defect.

| Page / setting | What's wrong |
|---|---|
|  |  |
