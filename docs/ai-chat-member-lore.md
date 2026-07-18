# AI chat — member lore / community personas

*Status: idea / design notes, not yet built. Captured while the hand-written version was
already delighting the community.*

## The vision

Lost Falcons has played together for ~6 years — they know each other well, some from real life.
The bot (Hoshi Sato) should feel like a **real member of that community**, not a generic assistant:
it knows the running jokes, teases the right people about the right things, and can riff about
anyone in the roster. This is warm, consensual, insider banter — the stuff the friend group already
jokes about openly — **not** clinical profiling.

It already works. Hand-written "lore" lives in the AI-chat **system prompt** today, e.g.:

> - *Frank darfst du zwischendurch gerne etwas hochnehmen, er mag Rotwein sehr.*
> - *justHek, auch Döni oder nur Hek genannt, isst gerne Kuchen und trinkt viel Bier … Wenn einer
>   es schafft, in jedes Fettnäpfchen zu treten, dann ist es Döni!*
> - *RagnarökSpeed oder einfach Speed, bei ihm ist der Name nicht Programm, da ist eher alles
>   Slowmotion!*
> - *Anorzul oder einfach Anor, der Vize-Admiral von LF … immer auf der Jagd nach frischem Wild …
>   Er liebt die Verrückten, zieht sie an, ob hier im Spiel oder im echten Leben!*

Observed live: the bot welcomed Anor back from vacation (picked up from Anor's own message), called
him "Vize-Admiral … auf der Jagd nach frischem Wild", and — while talking to a *different* member —
spontaneously name-dropped Frank sipping Rotwein and Döni heading for the next Fettnäpfchen.
Community reaction: *"Hoshi bringt wieder etwas Leben rein"*, *"Hoshi ist der beste Member."*

## What we learned from that (design-shaping)

1. **The hand-crafted lore is the magic.** It's specific, affectionate, in the house voice.
   Auto-generated text would be blander and occasionally land wrong. Humans should stay the authors.

2. **The charm is the *ensemble*, not just the present company.** The best moment (Frank + Döni)
   happened while Hoshi was talking to someone else — those two weren't in the exchange at all. A
   naive "inject only the current conversation's participants" scheme would have *killed that joke*.
   Feeling like a real member means knowing the whole cast, not just who's in the room. → **Do not
   over-scope the injection.**

3. **The real pain is maintenance, not capability.** Everything in one growing system-prompt blob is
   a never-ending story: manual, unstructured, no per-member editing, and it bloats every prompt.

## The core idea

Lift the lore out of the monolithic system prompt into a **per-member notes store**, keep injecting
it so the ensemble effect survives, and make it editable one member at a time.

- **Storage**: a small table, e.g. `GuildMemberNote(GuildId, DiscordUserId, Note text, UpdatedAt)`,
  one free-text note per member per guild. **Key on Discord user id**, not name — survives
  nickname/alliance-tag changes (`CommanderName.Of` already strips tags for display).
- **Injection point**: the same place that already assembles the *"Bekannte Nutzer"* block in
  `AiChatService.BuildSystemInstructionAsync` (`src/HoshiBot.Discord/AiChat/AiChatService.cs`). That
  method already computes the `mentionable` set (conversation participants → names + ids). Extend it
  to also emit each member's note. Mirrors the structured-facts pattern we already ship
  (`BuildTerritoryCaptureFactsAsync`).
- **Scope — keep the ensemble (per learning #2):** since notes are compact (a line or two), a
  guild's roster is cheap to include, so the bot can riff about anyone. A **hybrid** if it ever gets
  large: a **compact one-liner for everyone** (always available) plus a **richer note for whoever's
  actually in the conversation**. Only reach for heavy scoping if token cost becomes real.
- **Two kinds of note, two authors:**
  - **Self-bio** — member-authored ("Steckbrief"): who they are, what they're into. Consensual by
    construction (you write your own), accurate, and it offloads maintenance from staff.
  - **Peer lore** — staff/peer-authored: the running jokes and teasing angles (*"darf man wegen
    Rotwein necken"*, *"Döni + Fettnäpfchen"*). Frank won't write his own Rotwein jab — his friends
    do.

  Could be two fields on one `GuildMemberNote`, or two rows; both get injected into the prompt.
- **Editing surfaces** (this is what ends the "never-ending story"):
  - **Member self-service page** (easy, high-value): a Discord-authenticated page where a logged-in
    member sees/edits **their own** self-bio for the guild(s) they're in. Reuses the Web app's
    existing Discord OAuth (`AspNet.Security.OAuth.Discord`) and `DiscordUserGuildsService` (already
    used by the admin guild picker); unlike `/manage` it's scoped to *any member*, own-data-only — a
    new lightweight authorization policy, not admin-gated. Transparency is built in: members see
    exactly what the bot knows about them, which is most of the trust story.
  - **Staff editing** for the peer lore: the guild admin member list, and/or a `/note @member …`
    command.

## Learning from posts — as an *assist*, not the author (phase 2)

The corpus already exists: `AiChatIndexedMessage` (via `AiChatIndexService`) holds every
knowledge-channel message with author, timestamp, content, embedding — filterable per member. Use it
to *reduce authoring effort*, never to autonomously profile:

- **Suggestions**: "Frank mentioned Rotwein 12× this month — add to his note?" → human approves/edits.
- **Draft a starting note** from a member's posts that staff rewrite in the house voice.
- **Factual auto-fill** (objective, safe): active hours (from message timestamps), languages, and
  ops level / rank / alliance straight from `PlayerLinks → StfcPlayer` — no inference needed. (This
  overlaps with extending the structured-facts injection to members.)
- **Seed from the introductions channel** (best source): LF had a channel where members introduced
  themselves — rich, self-authored, shared-for-the-community bios, i.e. perfect consensual seed
  material. Wire an "introductions channel" setting; pull each member's intro to **pre-fill their
  self-bio as a draft** they then edit/approve on their page. Far cleaner than inferring from
  scattered chat.

Keep humans in the loop so the warmth and comedic timing stay intact.

## Collecting the data (this can be the fun part)

Several complementary ways to fill the notes — ordered best-first for *engagement*:

1. **Bot-run DM interview** *(the headline idea)* — Hoshi DMs a member and just chats, in character,
   to get to know them. The data-gathering **is** the delightful interaction the community already
   loves, so nobody experiences it as harvesting. Example opener:

   > *Hi, ich bin Hoshi und neu dabei bei euch. Um dich besser kennenzulernen, erzähl mir doch was
   > über dich. Was machst du so? Wie soll ich dich nennen? Hast du lustige Geschichten über andere
   > Spieler?*

   - Reuses the AI-chat pipeline in a **DM context** with an **"interview mode" system prompt** —
     multi-turn, natural follow-ups. An **extraction pass** (during/after) turns the free chat into
     note *drafts*: a self-bio + preferred name for the interviewee, and — the clever bit —
     **crowdsourced peer lore** from *"lustige Geschichten über andere Spieler"* (Frank's Rotwein jab
     can come from his friends, not just staff).
   - **Consent/mechanics**: bots can only DM users who share a guild, and some have DMs closed. So
     make it opt-in (a "Erzähl mir von dir" button / `/introduce`) or a *gentle* welcome-DM on join
     with an easy "nö danke" — never spammy; fall back to the self-service page for closed DMs.
   - **Peer stories get a beat of review**: a story one member tells about another lands as a *draft*
     on the target's peer-lore, staff-glanced before it goes live (weight/consent again — most is
     gold, the review just catches the occasional too-sharp one).
2. **Member self-service page** — the durable self-edit surface (see *Editing surfaces* above).
3. **Introductions-channel seed** — pre-fills self-bio drafts (see *Learning from posts* above).
4. **Passive post-analysis** — suggests additions from existing chat (see *Learning from posts*).

## Privacy / trust (lighter here, still worth it)

This is a private, consensual friend group, so the GDPR-profiling worry is much smaller than a
clinical trait catalog — but a couple of habits keep it feeling like an in-joke everyone's in on:

- **Factual vs. judgmental**: game-relevant/affectionate lore is fine; avoid storing harsh
  psychological/competence judgments.
- **Transparency**: let a member **see (and edit/veto) their own note** — the self-service page *is*
  this. Cheap goodwill; keeps trust.
- **Weight matters, not just consent.** Self-intros can carry *heavy* real-life info (a member's
  severely ill child, foster kids, jobs, locations). "Shared with the community" ≠ "OK for the bot
  to banter with." The self-service page is the safety valve: the member curates their own note, so
  *they* decide what's light/shareable. Auto-seed (from intros) should lean factual/light and always
  land as a member-editable **draft**, never go live unreviewed.
- **Human-authored / human-approved** content only (see phase 2) — no silent machine profiling.

## Rough shape / phases

- **Phase 1** — `GuildMemberNote` store (self-bio + peer-lore) + inject at the "Bekannte Nutzer"
  block (compact-for-all, optionally richer-for-participants). Migrate today's hand-written lore in
  as-is. Pure win: keeps all of today's magic, kills the maintenance pain.
- **Phase 1.5** — the **member self-service page** (Discord-auth, own-data-only) so members maintain
  their own self-bio; staff keep editing peer lore.
- **Phase 2** — the **bot-run DM interview** (interview-mode prompt in DMs + extraction pass →
  self-bio + crowdsourced peer-lore drafts); seed self-bios from the **introductions channel** as
  editable drafts; optional post-analysis that *suggests* note additions for human approval; factual
  auto-fill from `StfcPlayer`.

## Open questions (decide when planning)

- One free-text note per member, or a few structured fields (nicknames, interests, running jokes,
  "OK to tease about")? Free-text is simplest and matches the current voice; structure helps
  auto-assist.
- Who can edit — staff only, or any member for their own note?
- Compact-for-all vs. richer-for-participants: start simple (everyone, compact) and only split if
  prompt size actually bites.
- DM interview trigger: purely opt-in (button / `/introduce`) vs. a gentle proactive welcome-DM on
  join? And does the interviewer run as a distinct "interview mode" or just the normal persona in a
  DM?
- Peer-lore from DM stories: always staff-reviewed before going live, or auto-publish light/positive
  ones and only queue borderline for review?
- Feature toggle: gate behind a per-guild switch like the other AI-chat sub-features.

## Code touchpoints

- `src/HoshiBot.Discord/AiChat/AiChatService.cs` — `BuildSystemInstructionAsync` (the "Bekannte
  Nutzer" block + `mentionable` set) is where lore injects; `BuildTerritoryCaptureFactsAsync` is the
  pattern to copy.
- `AiChatIndexService` / `AiChatIndexedMessage` — per-member post corpus for phase-2 assist.
- `PlayerLinks` → `StfcPlayer`, `CommanderName.Of` — factual member data + display names.
- New: a `GuildMemberNote` entity + editor page/command; a `GuildFeature`/settings toggle if gated.
