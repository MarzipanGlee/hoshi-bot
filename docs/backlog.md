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

## Alliance emblems (icons)

Alliances have emblems; show them in the admin UI wherever an alliance appears.

- **Source:** stfc.pro's alliance data carries an `emblem` field (integer, e.g. `"emblem": 21`).
  Seen in `data/alliances/alliances` and the players feed's `allianceData`.
- **Assets:** `assets/emblems/Emblem_000.png` … `Emblem_027.png` (28 images). The `emblem`
  value looks like a direct 0-based index → `Emblem_{emblem:D3}.png`, **but confirm the mapping**
  (spot-check a few known alliances against their in-game emblem before relying on it).
- **Storage:** `StfcAlliance` doesn't store `emblem` today — add an `int Emblem` (or `int?`)
  column and populate it in the alliance sync/seed (`StfcAllianceSeedData` + whatever future
  live sync replaces it). It's a small snowflake-adjacent value; a plain int column is fine.
- **Serve the images:** copy `assets/emblems` into `HoshiBot.Web/wwwroot/emblems` (or add a
  static-file mapping) so they're web-servable, then reference `emblems/Emblem_0XX.png`.
- **Display:** show the emblem next to the tag/name in `AllianceCard.razor`,
  `AllianceSelector.razor`, and the sidebar's Alliance group — mirrors how `GuildIcon` renders
  a guild's avatar. A small rounded/framed `<img>` with a graceful fallback when the emblem is
  unknown.
- Ties into the multi-alliance work (per-alliance selector/cards/nav) — the emblem is the
  natural visual anchor for "which alliance am I looking at".
