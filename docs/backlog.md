# Backlog

Deferred ideas / follow-ups, not scheduled yet.

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
`HoshiBot.Web/wwwroot/emblems/Emblem_{n:D3}.png` (0–26); rendered via the reusable
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
