# Backend review checklist

Every admin page and every setting it shows, to tick off while reviewing the backend.

Generated from the code, not written by hand: the feature list comes from `GuildFeature`, the
cards from what each editor actually renders, and the labels from the English catalog. If a card
is missing here it is missing from the page too — worth reporting either way.

Scope column: which audiences a feature can be configured for (`Alliance`, `Server`, `Community`,
`VeilGroup`, `Guild` = guild-wide). A multi-audience feature needs checking once per audience the
guild actually serves.

**Legend** — `[ ]` not looked at · `[x]` checked, fine · add `⚠` and a note for anything wrong.

---

## 1. Guild pages

- [ ] **Overview** — `/manage/guild/{id}`
- [ ] **Audience** — `/manage/guild/{id}/audience`
- [ ] **Linked Alliances** — `/manage/guild/{id}/alliances`
- [ ] **Features (guild-wide)** — `/manage/guild/{id}/features`
- [ ] **Setup Wizard** — `/manage/guild/{id}/setup-wizard`
- [ ] **Permission Check** — `/manage/guild/{id}/permission-check`

### Guild settings — `/manage/guild/{id}/settings`

- [ ] Log
- [ ] Admin
- [ ] User Log
- [ ] Crews
- [ ] Language

## 2. Alliance pages

Per linked alliance. A coalition guild must check each one — several settings are per-alliance.

- [ ] **Overview** — `/manage/guild/{id}/alliance/{allianceId}`
- [ ] **Features** — `/manage/guild/{id}/alliance/{allianceId}/features`

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

## 3. Audience pages

Once per audience the guild serves (Server, Community, Veil Group).

- [ ] **Features** — `/manage/guild/{id}/{audience}/features`

### Audience settings — `/manage/guild/{id}/{audience}/settings`

- [ ] Nothing to link
- [ ] Default Channel Category
- [ ] Senior Staff Role
- [ ] Language

## 4. Features

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

### AI Provider

`ai-backend` · Guild

- [ ] Enable switch, description and any "requires" badges
- [ ] AI provider
- [ ] Gemini API key
- [ ] Model (optional)
- [ ] Gate model (optional)
- [ ] Router model (optional)
- [ ] Embeddings (optional)
- [ ] Image embeddings (optional)

### Absences

`absences` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Absences Report
- [ ] Absences Report (Staff)
- [ ] Notification Role

### Alliance Tag Roles

`alliance-tag-roles` · Guild

- [ ] Enable switch, description and any "requires" badges
- [ ] How it works
- [ ] Create Missing Roles
- [ ] Plain Letters
- [ ] Lower Case
- [ ] Prefix and Suffix
- [ ] Foreign-Alliance Role
- [ ] No-Alliance Role
- [ ] Roles in use

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

### Diplomacy

`diplomacy` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] Diplomacy
- [ ] Diplomat Role

### Hoshi Say

`hoshi-say` · Guild

- [ ] Enable switch, description and any "requires" badges
- [ ] Allowed role

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

### Member Onboarding

`member-onboarding` · Community

- [ ] Enable switch, description and any "requires" badges
- [ ] How it works
- [ ] DMs per day
- [ ] Campaign

### Nickname Sync

`nickname-sync` · Guild

- [ ] Enable switch, description and any "requires" badges
- [ ] How it works
- [ ] Alliance tag
- [ ] Server tag
- [ ] Member Name Suffix
- [ ] Excluded roles
- [ ] Add role
- [ ] false

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

### Territory Capture Sign-Off

`territory-capture-sign-off` · Alliance

- [ ] Enable switch, description and any "requires" badges
- [ ] No settings of its own — the switch is the whole configuration

### Tickets

`tickets` · Alliance+Server+VeilGroup+Community

- [ ] Enable switch, description and any "requires" badges
- [ ] Tickets

## 5. Bot administration

- [ ] **Global Admins** — `/manage/bot/global-admins`
- [ ] **Trusted Users** — `/manage/bot/trusted-users`
- [ ] **STFC News settings** — `/manage/bot/stfc-news-settings`
- [ ] **Incursions schedule** — `/manage/bot/incursions-schedule`

## 6. STFC data

Shared game data, not per guild. List/create/edit/delete per area — checking the list and one
edit is usually enough.

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

## 7. Database browser

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

## 8. Member-facing

- [ ] **My profile** — `/me`
- [ ] **My lore** — `/me/lore`
- [ ] **Home / login** — `/`
- [ ] **Not found** — `/not-found`

---

## Notes

Anything found goes here, or straight into [backlog.md](backlog.md) if it is a deferred idea
rather than a defect.

| Page / setting | What's wrong |
|---|---|
|  |  |
