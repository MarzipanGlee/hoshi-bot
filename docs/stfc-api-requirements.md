# STFC Data API — Requirements for Hoshi Bot

**Audience:** the developer of `stfc.pro` / `api.stfc.pro`
**From:** the Hoshi Bot project (a Discord bot for Star Trek Fleet Command alliances)
**Status:** requirements draft — endpoint shapes below are *proposals* based on how Hoshi
Bot already models this data; the actual endpoint names, schema, and auth are yours to decide.

---

## 1. What Hoshi Bot is, and why we're asking

Hoshi Bot is a Discord bot used by STFC alliances to run day-to-day operations: server
up/down alerts, Infinite Incursions and Alliance Tournament announcements, territory-capture
digests, member nickname/rank/ops-level role syncing, and an admin web panel. To do this it
needs a **reliable, machine-readable, and explicitly permitted** source of live STFC game
data.

We already model much of this data internally against **stfc.pro's stable IDs** — our
player, alliance, and territory records each carry an `ExternalId` that is *your* site's ID,
specifically so we can re-sync and match the same entity over time (players rename, alliances
re-tag). Territory IDs in our seed data were reconciled directly against
`https://api.stfc.pro/stfc_territories`. So in a real sense we've already built toward your
API being the source of truth.

**The blocker:** `stfc.pro`'s `robots.txt` currently disallows `/api/` for automated agents.
We deliberately respect that, which is why several features today run off **one-time manual
snapshots** that go stale, instead of a live sync. This document lists exactly what we'd pull
if there were an official, permitted endpoint (`api.stfc.pro`) — so you can see the whole
surface at once and decide what you're able to offer.

---

## 2. Priority summary

This is the single overview of every dataset: what it's for, how important it is, and where it
comes from *today*. Detailed field/endpoint specs follow in §4.

| # | Data need | Why it matters | Priority | Fetched today from *(stale unless noted)* |
|---|-----------|----------------|----------|--------------------------------------------|
| C | **Alliances** (per server) | Directory, tag→name resolution, diplomacy, ownership | **P1** | Manual snapshot · `stfc.pro/api/alliances` (~10k) |
| D | **Players** (per server, + rank + ops level) | Nickname / rank / ops role sync, name history | **P1** | Manual snapshot, server 164 only · `stfc.pro/api/players` (~1.5k) |
| A | **Server status** (up / down / maintenance) | Real-time down/maintenance alerts to Discord | P2 | Manual snapshot · `stfc.pro/api/server-status` |
| B | **Event schedule** (Incursions, Alliance Tournament, …) | Announce event start/end; region-split start times | P2 | Manual snapshot · `stfc.pro/api/events` + WordPress RSS |
| E | **Server / region / veil-group catalog** | Master list of servers; new servers launch regularly | P2 | Region/veil/number already in `/api/alliances` (`groupname`); only the server **name** is scraped from `stfc.pro/servers` |
| F | **Territory ownership** (per server, per zone) | Territory-capture digests: who owns which zone | P2 | Snapshot, server 164 only, of `api.stfc.pro/stfc_territories` (zones + per-server owners) |
| G | **Alliance diplomacy** (alliance → alliance stance) | Show diplomatic status in digests / commands | P2 | **Manual** — leaders enter it in Discord; *no endpoint found* |
| — | Systems, client versions, news | *(already permitted & live — see §5)* | n/a | **Live**: `data.stfc.space`, Xsolla/Play/iTunes, WordPress feed |

**Start here: C (Alliances) and D (Players)** — these back the features our members touch most
(directory, nickname/rank/ops role sync). Everything else is a valuable P2 follow-up.

**Every P1/P2 row except G already comes from a stfc.pro endpoint you run** — including **F**,
whose per-server ownership we read straight from `stfc_territories`. We captured each *once, by
hand*, then stopped, because polling `/api/` from an automated agent is disallowed by your
`robots.txt`, which we respect. So most of this request isn't "please build new endpoints"; it's
**"please permit and document automated access to endpoints you already have."** The remaining
gaps are small: **E** is *mostly* already in `/api/alliances` (region + veil group + number) — only
the human-readable server **name** is page-only — and **G** (alliance diplomacy) is the one thing
we've found no endpoint for at all. Rows are lettered A–G to match §4, not ordered by priority.

---

## 3. Cross-cutting requirements (apply to every endpoint)

- **Transport:** JSON over HTTPS. Plain `GET`, no session/cookies.
- **Permitted automated access.** The single most important ask: a documented, `robots.txt`-
  and ToS-permitted way for a server-to-server bot to poll these endpoints. We will send a
  descriptive `User-Agent` (e.g. `HoshiBot/1.0 (+https://stfc.bot; contact@…)`) and honor
  whatever identification/registration you require.
- **Auth:** we're happy to use an API key / token (header or query param) if you want to gate
  and attribute traffic. Tell us how to request one.
- **Rate limits:** please document them. Our required cadences are modest (see each section) —
  the most frequent is server status at ~60 s. We'll honor `Retry-After` / `429`.
- **Stable numeric IDs.** We already depend on stable per-entity IDs (`player.id`,
  `alliance.id`, `territory.id`, `server` number). These must not be recycled — a rename or
  re-tag must keep the same ID. This is what makes incremental re-sync possible.
- **Efficient re-fetch.** For the large collections (alliances, players) we want to avoid
  re-downloading everything each poll. You already expose **`https://stfc.pro/api/schedules`**,
  which reports when each dataset was last updated — that's an ideal fit: we can poll that
  cheap endpoint and only re-pull a heavy collection when its update timestamp actually
  changes, **in place of `ETag`/`304` conditional requests**. (`ETag`/`Last-Modified` + `304`,
  an `updated_since` query param, or per-server scoping would each work too, but the
  `schedules` endpoint already gives us what we need.) Full dumps remain an acceptable
  fallback.
- **Timestamps in UTC**, ISO-8601 (`2026-06-20T15:00:00Z`).

---

## 4. Detailed data needs

For each: what we use it for, our current (stale) source, the fields we need, a **proposed**
response shape, refresh cadence, and the match key.

### A. Server status — up / down / maintenance  ·  P2 (blocked)

- **Use:** a job announces to each guild's alert channel when one of their tracked servers
  goes down or enters/leaves maintenance. Diffed against the last-announced state.
- **Current source:** a one-time snapshot captured 2026-07-08 from **`https://stfc.pro/api/server-status`**.
  Static; never updates.
- **Volume:** 113 known servers today (growing).
- **Cadence:** **every ~60 seconds** (our most frequent poll). If that's too aggressive for
  your infra, tell us a floor and we'll back off — even 1–2 min is far better than a frozen
  snapshot. A single "all servers" response is ideal so it's one request per poll, not 113.
- **Fields per server:**
  - server ID (the global STFC server number, e.g. `8`, `164`)
  - status — an up/down/maintenance indicator. *We currently model this as an integer
    (`1` = up in our snapshot).* **Please document the full set of possible values.**
  - maintenance — whether/when maintenance is active. *We currently store this as a string
    (`"0"` = none in our snapshot); we're unsure of its full semantics.* **Please document
    what this field actually represents** (a boolean? a window? a message?).
  - last-updated timestamp (optional but useful)
- **Proposed shape:**
  ```json
  GET /api/server_status
  [
    { "server": 8,   "status": "up",          "maintenance": null,                              "updated_at": "2026-07-13T08:00:00Z" },
    { "server": 164, "status": "maintenance", "maintenance": { "until": "2026-07-13T10:00:00Z" }, "updated_at": "2026-07-13T08:00:00Z" }
  ]
  ```
- **Match key:** server number.

### B. Event schedule — Incursions, Alliance Tournament, etc.  ·  P2

- **Use:** advance-warning announcements ("Infinite Incursions starts in your region at …")
  and event-active state. Today we detect these from the official WordPress news feed and
  then **crowd-source the exact date/time from Discord users** with a confirmation quorum — a
  workaround precisely because we have no authoritative schedule feed.
- **Current source:** a one-time snapshot from **`https://stfc.pro/api/events`** + WordPress RSS
  detection + manual Discord confirmation. (If `/api/events` already carries authoritative,
  per-region start/end times, that alone could retire the RSS + crowd-sourcing path entirely.)
- **Event groups we care about (extensible):** `incursions` (Infinite Incursions),
  `alliance_tournaments`, and we also store `sarris_invasions`, `flashpoint` for completeness.
- **Region split:** Infinite Incursions runs at **different start times per region** — we
  confirmed US 15:00 UTC, EU 08:00 UTC, APAC 23:00 UTC, each a 12-hour event. So the schedule
  needs to express **per-region** start times for region-split events (a single global time
  is silently wrong for 2 of 3 regions). Our 3 regions are US, EU, APAC.
- **Advance visibility — the key question for this endpoint:** our whole use case is a warning
  sent *before* an event begins, so we need upcoming events to appear in `/api/events` **ahead
  of their start time**, not only once they're live. If `/api/events` only ever reflects the
  currently-running event, advance warnings aren't possible and we'd stay on the crowd-sourcing
  workaround. **How far in advance are new events published there?**
- **Fields per (event group [, region]):**
  - event group key
  - region (for region-split events; null/global otherwise)
  - start (UTC), end (UTC)
  - active (bool) — or we can derive it from start/end + now
- **Cadence:** every ~1–5 min is plenty (these change on the order of hours/days).
- **Proposed shape:**
  ```json
  GET /api/events
  [
    { "group": "incursions", "region": "US",   "start": "2026-06-20T15:00:00Z", "end": "2026-06-21T03:00:00Z" },
    { "group": "incursions", "region": "EU",   "start": "2026-06-20T08:00:00Z", "end": "2026-06-20T20:00:00Z" },
    { "group": "incursions", "region": "APAC", "start": "2026-06-20T23:00:00Z", "end": "2026-06-21T11:00:00Z" },
    { "group": "alliance_tournaments", "region": null, "start": "2026-05-05T17:00:00Z", "end": "2026-05-10T17:00:00Z" }
  ]
  ```
- **This one endpoint would let us retire the entire crowd-sourcing workaround** — high value.

### C. Alliances — per server  ·  **P1**

- **Use:** alliance directory, resolving a tag to a name, diplomacy tracking, territory-owner
  display. Shared across all guilds.
- **Current source:** a one-time capture from **`https://stfc.pro/api/alliances`**, ~10,045
  alliances. The payload is rich — per alliance it returns `id`, `tag`, `name`, `server`,
  `region`, `groupname` (the veil group), plus rank/power stats. We only persist the identity
  fields today, but note `region` + `groupname` + `server` here are also what let us build the
  entire **server/region/veil catalog** (see §E) — this endpoint does double duty.
- **We track name/tag history**, so a periodic re-sync lets us record when an alliance
  re-tags or renames.
- **Fields per alliance:** stable ID (`id`, int64 — values exceed 2³¹, so please keep 64-bit),
  `server` number, `tag`, `name`. (Member count / power optional, not required.)
- **Cadence:** daily is fine.
- **Proposed shape:**
  ```json
  GET /api/alliances?server=164          // or a full dump, paged
  [ { "id": 363647450, "server": 8, "tag": "ABC", "name": "ABC123" }, … ]
  ```
- **Match key:** alliance ID. Note **tags are only unique per-server**, not globally.

### D. Players — per server, with rank + ops level  ·  **P1**

- **Use:** Discord nickname sync, in-alliance **rank** role sync, **ops-level** role sync,
  and name-change history/audit. This is the richest ongoing need.
- **Current source:** a one-time capture from **`https://stfc.pro/api/players`**
  (`?sortBy=max_power&sortOrder=desc&page=1&pageCount=100&reRank=false` — so this endpoint
  already supports paging, which is great), ~1,491 players **for server 164 only**. **Good news:
  the payload already contains every field we need** — we simply hadn't wired them all into our
  snapshot yet. Per player it returns `playerid`, `owner` (the player name), `allianceid`, `tag`,
  `server`, `region`, `rankid`, `rankdesc`, and `level`. So this is purely a permitted-access
  need, not a schema change.
- **Fields per player (mapped to the fields we saw in the payload):**
  - stable player ID (int64) → `playerid`
  - name (current in-game name) → `owner`
  - server number → `server`; region → `region`
  - alliance they belong to → `allianceid` (+ `tag`)
  - **rank** — STFC's fixed 5-tier in-alliance rank → `rankid` **1–5**
    (`1 = Admiral, 2 = Commodore, 3 = Premier, 4 = Operative, 5 = Agent`) — already an exact match.
  - **ops level** — the raw **1–80** Ops Level integer → `level` (we bucket it into G1–G7
    ourselves, so the raw number is exactly right).
- **Cadence:** daily is fine; more often is welcome for nickname/rank freshness but not
  required.
- **Proposed shape:**
  ```json
  GET /api/players?server=164            // or ?alliance=<id>, or a full dump, paged
  [ { "id": 25113752, "name": "oxGAMBITxo", "server": 164, "alliance": 1789215522, "rankid": 4, "ops_level": 52 }, … ]
  ```
- **Match key:** player ID (names change — that's exactly why we key on the ID).
- **Privacy note:** we only need players relevant to the alliances our guilds track. A
  per-server or per-alliance scope (rather than a global player dump) would be both lighter
  and more privacy-appropriate — your call.

### E. Server / region / veil-group catalog  ·  P2

- **Use:** the master list of every real server, its region, and its "veil group" cluster —
  used in every server dropdown and to attach alliances/players to servers.
- **Mostly already solved by `/api/alliances`.** Each alliance record carries `server` (number),
  `region` (`"EU"`), and `groupname` (`"EU-4"` — the veil group). We verified against the full
  alliance dump: **all 113 servers, all 3 regions, and all 6 veil groups are present**, with
  `groupname` correctly `null` for the handful of brand-new servers not yet assigned one. So we
  can build the region/veil/number catalog straight from the alliance data we already pull for
  §C — **no separate endpoint needed for those fields.** (`/api/players` has the same
  `server`+`region` per row and corroborates it.)
- **The one gap: the human-readable server name** (e.g. "Saladin", "Sol", "Mindmeld"). It's not
  in `/api/alliances`, `/api/players`, or `/api/server-status` — `server-status` only carries the
  `"EU-164"` region-number label, not the galaxy name. Today the real names come *only* from
  scraping the rendered **`https://stfc.pro/servers`** page.
- **So the actual ask here is small:** expose the **server display name** in JSON, keyed by
  server number — ideally folded into `/api/alliances` (a server block) or a tiny `/api/servers`.
  If it's simply not in your dataset, we can fall back to showing `"<REGION><number>"`.
- **Cadence:** weekly/on-demand is plenty.
- **Proposed shape (only the name is new; the rest we already derive from `/api/alliances`):**
  ```json
  GET /api/servers
  [ { "number": 164, "name": "Mindmeld", "region": "EU", "veil_group": "EU-4" }, … ]
  ```
- **Match key:** server number. **Coverage caveat:** alliance/player data only includes servers
  that have alliances/tracked players — a truly empty brand-new server wouldn't appear until it
  does (fine for our purposes).
- Note: server **name is only unique within a veil group**, not globally (e.g. "Tanagra"
  exists in both EU-4 and APAC-6) — the number is the real key.

### F. Territory ownership — per server, per zone  ·  P2

- **Use:** the Territory-Capture digest shows who currently owns each contestable zone (and
  its neighbours) on a given server.
- **Current source:** **`api.stfc.pro/stfc_territories`** (the API behind territory.lol) — it
  provides both the zone definitions *and* per-server ownership tags; we read the ownership
  straight from it. Today we only hold a one-time snapshot for **one** server (164), captured
  2026-07-05, because we can't poll it automatically — the ownership is dynamic and per-server,
  so a single frozen server is very limiting.
- **This is a "permit automated polling" ask, not a "build an endpoint" ask** — the data is
  already there. Two parts we read from it:
  1. **Zone catalog** — territory ID, name, tier, and (if known) the weekly capture weekday +
     UTC time, and neighbour/adjacency list.
  2. **Ownership** (the part that goes stale): for each server, which **alliance** currently
     owns each **territory**, and (if available) when it was last captured.
- **Fields per ownership row:** server number, territory ID, owning alliance ID,
  last-captured timestamp (if available).
- **Cadence:** hourly/daily.
- **Proposed shape:**
  ```json
  GET /api/territory_ownership?server=164
  [ { "server": 164, "territory": 620533187, "alliance": 1789215522, "last_captured_at": "2026-07-12T09:00:00Z" }, … ]
  ```
- **Match keys:** territory ID + server number → alliance ID. (These are the same territory
  IDs already in `api.stfc.pro/stfc_territories`, so they line up with what you have.)

### G. Alliance diplomacy — alliance → alliance stance  ·  P2 (may not be available)

- **Use:** show the diplomatic stance one alliance holds toward another (e.g. ally / enemy /
  neutral) in territory digests and commands. It's an in-game, alliance-to-alliance fact,
  independent of any Discord guild's opinion.
- **Current source:** **manual input from alliance leaders** inside Discord — we have *not*
  found any API endpoint that exposes in-game diplomacy, so this is entirely hand-maintained
  today. If stfc.pro has access to this data, an endpoint would remove a real manual burden;
  if it's simply not available from the game's public data, that's useful for us to know too so
  we can stop looking.
- **Fields per row (if available):** source alliance ID, target alliance ID, status.
- **Cadence:** daily is plenty.
- **Proposed shape:**
  ```json
  GET /api/alliance_diplomacy?server=164
  [ { "source_alliance": 1789215522, "target_alliance": 363647450, "status": "enemy" }, … ]
  ```
- **Match key:** (source alliance ID, target alliance ID). See open question in §6.

---

## 5. Already sourced elsewhere — *not* requesting (FYI)

These work today from permitted, non-stfc.pro sources. Listed only so you have the full
picture; we'd only move them to your API if you'd prefer to be the single source.

- **Systems catalog** (2,596 systems: id, name, "station housing" flag) — live daily from
  `data.stfc.space` (`https://data.stfc.space/system/summary.json` +
  `https://data.stfc.space/translations/en/systems.json`), which is Scopely-operated and
  imposes no `robots.txt` restriction.
- **Game client versions** (Windows/macOS/Android/iOS), polled every 60 s — live from Xsolla
  (`https://gus.xsolla.com/updates?project_id=152033&platform={platform}`), the Play Store
  (`https://play.google.com/store/apps/details?id=com.scopely.startrek`), and the iTunes
  Lookup API (`https://itunes.apple.com/lookup?id=1427744264`).
- **News posts**, polled every 30 min — live from the official
  `https://startrekfleetcommand.com/feed/` WordPress RSS.

---

## 6. Open questions for you

1. Can `api.stfc.pro` be made **explicitly permitted** for an identified server-side bot
   (robots.txt / ToS), and how should we register / identify ourselves?
2. Is there an **API key** scheme? How do we request one?
3. What are the **rate limits**, and is a ~60-second **server-status** poll acceptable (or
   what's the floor)?
4. **Server status:** what is the full set of `status` values, and what does the
   `maintenance` field actually represent?
5. **Players (P1):** `/api/players` already returns everything we need — `playerid`, `owner`
   (name), `allianceid`, `server`, `region`, `rankid` (1–5), `rankdesc`, and `level` (ops 1–80).
   So for us this is purely a **permitted-access** question, no schema change. Are those fields
   stable?
6. **Server name:** the region, veil group (`groupname`), and number for the whole server
   catalog are all already in `/api/alliances` — the only field we can't get from JSON is each
   server's human-readable **name** (e.g. "Saladin"), which we currently scrape from `/servers`.
   Is that name available in JSON anywhere, or could it be added (to `/api/alliances` or a small
   `/api/servers`)?
7. **Events:** does `/api/events` carry authoritative, forward-looking start/end times —
   including **per-region** Incursions times (US/EU/APAC)? And crucially, **how far in advance
   are new events published there** — ahead of start (so we can warn), or only once live?
8. **Alliance diplomacy:** do you have access to in-game **alliance-to-alliance diplomacy**
   (who's allied/hostile to whom), via any endpoint? We've found none, so this is fully
   manual for us today — even a "no, that's not in the public data" is a useful answer.
9. Does **`/api/schedules`** cover the heavy datasets we'd poll (players, alliances,
   territory ownership, server status) so we can use it to decide when to re-fetch? And can
   we additionally scope those by **server** to keep each pull small?
10. **Territory ownership:** we already read per-server owners from `stfc_territories` — is that
    the right long-term endpoint to poll for it (and is the per-server owner a stable
    machine-readable field, not just an SVG tag we scraped)?

---

*Thanks for building and maintaining stfc.pro — a permitted API would let Hoshi Bot replace
several stale manual snapshots with proper live data, and we've intentionally built our data
model around your stable IDs so integration should be straightforward.*
