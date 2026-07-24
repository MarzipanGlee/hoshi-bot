# AI chat — reliability & retrieval roadmap

*Status: phased plan. Captured 2026-07-24 after a two-day live debugging session (the testing
guild), not a hypothetical — every incident below actually happened. Phases 0–3 are done; Phases
4–6 are not built yet.*

## Why this roadmap

Debugging one user-reported "Hoshi is hallucinating" incident uncovered four *stacked*, distinct
failure classes in the AiChat RAG pipeline — not one bug:

1. A grounding/prompt-wording gap (partial fix, insufficient alone).
2. A real index-hygiene bug: the backfill job had no bot-author guard, so Hoshi's own past wrong
   answers got indexed as "knowledge" and cited back to herself (self-reinforcing loop) — fixed
   (`1628feb`).
3. An embedding-provider outage (Gemini free-tier daily quota, then prepay-credit depletion) that
   silently degraded a guild to FTS-only for 24+ hours, discovered only by manually grepping bot
   logs and querying Postgres by hand, repeatedly.
4. A per-guild channel-tier misconfiguration: the channel where time-sensitive official
   announcements actually land wasn't marked "Preferred," so it never got the recency-safe
   "always fetch live" treatment and lost to stale content in ranked search — fixed via the Web
   admin channel picker once diagnosed.

Every one of these took direct SSH + log-grepping + raw `psql` to diagnose, because nothing in the
system surfaces AiChat's retrieval health to an operator, and the ranking algorithm has no
built-in defense against "stale content that reads similarly to a fresh fact." This roadmap closes
those systemic gaps — reliability of the existing pipeline, not new user-facing features — so the
next incident like this is visible and diagnosable in the Web admin, not a repeat SSH session.

## Phase 0 — Split guild-wide AI backend config out of AiChat — DONE

Prerequisite cleanup that landed before the phases below (commit `37fa31e`, deployed + verified on
the testing guild 2026-07-24). Every scalar AiChat setting was stored guild-wide
(`GuildAudience.None`) yet surfaced through the *per-audience* AiChat feature editor — credentials
written guild-wide from a tab that pretended to be per-audience. Extracted the backend bucket
(provider, API key, model, gate/router/member-lore models, embeddings) into a new guild-wide
`AiBackend` feature (Guild audience) that AiChat, MemberLore and AnnouncementForwarder all depend
on; the remaining behavioral settings (system prompt, search language, memory toggle, streaming)
became genuinely per-audience, with `AiChatService` resolving the incoming message's audience from
its channel (primary-alliance fallback for the Alliance audience). Data migration
`SplitAiBackendSettings` moved existing rows and auto-enabled `AiBackend` for AI-using guilds.
Doing this first means Phase 1's health page reads clean, separated feature boundaries.

## Phase 1 — AiChat health & observability (Web admin) — DONE

Shipped (commits `a9d003f` + `4cef712`, deployed + verified on the testing guild 2026-07-24). New
read-only "AI Chat health" page (ExtraPage on the AiChat feature) with three sections: embedding
coverage (indexed vs embedded + progress bar), provider health (last chat/embed success + error
time/message/model per guild, degraded/healthy badge), and the configured knowledge/listen channel
tiers. Backed by a new `AiChatProviderHealth` entity + `AiChatHealthService` (in `HoshiBot.Data`);
the bot records outcomes at the caller level (`AiChatService` for chat,
`AiChatIndexService.EmbedPendingAsync` for embeddings), with the embedding return enriched to
`EmbeddingBatchResult(vectors, error)` so the provider's own message (e.g. the quota/billing text)
surfaces. Verified live: an Embed success row and a Chat success row both wrote automatically.
Deferred from the original plan: the self-citation tripwire (needs the bot's guild display name,
which lives behind Discord-only helpers the Web project can't reference; the actual regression
guard already exists in code, `1628feb`).

**Why first:** every other phase either depends on being able to *see* the problem it fixes, or
is itself hard to justify without data (e.g., "do we need an ANN index yet?"). Also the cheapest
phase, and the one that would have made incidents #2–#4 self-diagnosable without an SSH session.

**Shape:** a new `ExtraPage` on `AiChatFeature.cs` (alongside the existing `"memories" →
MemoryAdmin.razor`), e.g. `"health" → AiChatHealthAdmin.razor`, showing per guild:

- **Embedding coverage**: indexed message count vs. embedded count (`AiChatIndexedMessages` where
  `Embedding IS NULL`), current embedding model/provider.
- **Provider health**: last error + timestamp per provider call class (chat / embed), so a quota
  or billing failure is visible without grepping logs. Needs a small new addition — an
  `AiChatProviderHealth(GuildId, Kind, LastErrorAt, LastErrorMessage, LastSuccessAt)` row, written
  from the existing catch blocks in `GeminiEmbeddingProvider.cs`, `GeminiClient.cs`,
  `OllamaClient.cs` (they already catch and log; add one write alongside the log call).
  `AiChatIndexJob.cs`'s per-run summary log line is the other natural write site for the backfill/
  embed-pass results.
- **Channel tier list**: which channels are configured Normal/Preferred/LastResort
  (`GuildFeatureChannels` Feature 18/19/20) with resolved channel names, so "is this channel
  Preferred?" is a glance, not a `psql` query.
- **Self-citation sanity check**: a count of `AiChatIndexedMessages` rows whose `AuthorName`
  matches the bot's own display name (should always be zero post-fix) — cheap regression
  tripwire for the exact bug class from incident #2.

**Code touchpoints:** `src/HoshiBot.Web/Components/Pages/Manage/Guild/Features/AiChat/*` (mirror
`MemoryAdmin.razor`'s pattern), `AiChatFeature.cs:22-23` (add the `ExtraPage` entry), new
`AiChatProviderHealth` entity + migration, `AiChatIndexService.cs`/`GeminiEmbeddingProvider.cs`/
`GeminiClient.cs`/`OllamaClient.cs` (write health rows alongside existing catch/log sites).

## Phase 2 — Recency-aware ranking — DONE

Shipped (commit `773a10d`, deployed + verified 2026-07-24). A third RRF term ranks the
already-retrieved FTS+vector candidate union newest-first at a fractional weight (`RecencyWeight =
0.5`) in `AiChatIndexService.SearchAsync`, so a fresher relevant row wins a near-tie without
introducing recent-but-irrelevant rows or burying a lone evergreen hit. A **forum-title search gap**
surfaced during verification and rode along (commit `b8a872a`): thread titles live in `ChannelName`
and were invisible to search, so a title-only query couldn't retrieve the post — fixed by matching
FTS over `Content + ChannelName` (immediate, all rows; SQL-verified) and prepending `ChannelName` to
the embedded text (semantic, forward). Verification of this phase also exposed the memory-grounding
problem now tracked as Phase 3 below.

**Why:** incident #4's root cause generalizes — the hybrid search's Reciprocal Rank Fusion has
**no time-decay at all**, so any channel (not just ones an admin forgot to mark Preferred) can
lose a fresh fact to an older, more textually-similar one. Channel-tier promotion is a per-channel
workaround; this is the systemic fix.

**Shape:** fold recency into `AiChatIndexService.SearchAsync`'s fusion, consistent with the
existing RRF approach rather than an ad-hoc score multiplier — e.g. a third candidate list ranked
purely by `CreatedAt DESC`, fused in via the same `1.0 / (RrfK + rank + 1)` formula alongside the
FTS and vector lists. Keeps the "score += ..." pattern already in place instead of introducing a
second scoring mechanism to reason about.

**Open question to resolve during design:** recency should probably help time-sensitive content
(patch notes, promo codes, event announcements) without hurting evergreen reference material (a
fixed crew-building guide, a rules doc) — a blanket decay could make old-but-still-correct answers
rank worse. Worth considering scoping the recency boost to specific channel tiers/categories
rather than applying it globally, or making the decay half-life long enough that it only breaks
near-ties rather than dominating relevance.

**Research note (embedding model choice, tangential but relevant):** two external write-ups on
2026 RAG embedding models were reviewed for anything applicable. Neither addresses quota/billing
reliability (the actual driver of this roadmap), but two points are worth carrying forward as
context, not action items:

- One incident this roadmap responds to was fundamentally a **cross-lingual** miss — a German
  query ("Überholung") failed to lexically match an English correction ("Refits") in plain FTS,
  and the semantic/vector leg that could have bridged that gap was unavailable (embedding-quota
  outage, see Phase 4). Both articles flag multilingual strength as a real differentiator between
  embedding models (one specifically calls out BGE-M3 as outperforming for mixed-language
  content) — worth keeping in mind if `embeddinggemma`/`gemini-embedding-2` ever show a pattern of
  missing German-query-to-English-content matches once Phase 1's observability exists to actually
  measure it. Not proposing a model change now — no evidence yet that the current model is weak
  here specifically, only that this failure mode has a known lever if it recurs.
- Both confirm `gemini-embedding-2` is genuinely multimodal (text/image/video/audio/PDF), and one
  source suggests 1024 dims loses very little recall vs. the native 3072 while cutting storage
  ~3x, with quality falling off faster below 512 — i.e. our current 768 is comfortably above the
  falloff point, not a red flag, just not necessarily the optimal point either. Neither is urgent
  enough to change the fixed `vector(768)` column now (would need a dimension migration + full
  re-embed per this doc's "Larger / different embedding model" note, see `docs/backlog.md`).

## Phase 3 — Memory grounding & confabulation guard — DONE

Shipped (commit `112d7f8`) + a retrieval title-weighting fix that verification exposed as the final
piece (commit `d093ca2`); deployed + verified end-to-end on the testing guild 2026-07-24 (Hoshi now
answers "Was ist die Unsterblichkeits-Crew?" with the correct Old Mudd / Ro Mudd / Eurydice crew
from the authoritative post — no confabulated crew or date). What it took, and the lesson: the
memory prompt fixes (below) were necessary but **not sufficient** on their own. Chasing the last
wrong answer surfaced three more layers stacked under the memory one:

- **Memory prompt fixes (the phase proper):** `MemoryExtractor.ExtractAsync` now captures only
  genuine *social* events, explicitly rejects speculation/questions/rumors, and refuses to store
  game mechanics/crews/builds/stats; the conversation summarizer marks speculation as speculation.
  `AiChatService.BuildSystemInstructionAsync` reframes episodic memory as soft recollections that
  must yield to retrieved sources, with an explicit precedence line (sources/facts/announcements
  outrank memory for factual questions).
- **Leftover polluted memories** had to be hand-deleted — the extraction fix only stops *new* bad
  memories; existing confabulated rows persisted and kept feeding the wrong answer.
- **Conversational echo:** the bot's own recent wrong answers sit in the live 15-message context
  window and get echoed forward within a session even after memory is clean — a fresh context (or
  removing the wrong messages) is needed to fairly evaluate.
- **Retrieval ranking (the actual last blocker):** even with memory clean, the authoritative post
  wasn't retrieved — folding `ChannelName` into FTS (Phase 2) made titles *matchable* but at equal
  weight with the body, so a specific post lost to recent/Preferred general chatter sharing a common
  word ("crew"). Fixed by weighting the channel-name (title) `'A'` and the body `'B'` via `setweight`
  in `FtsCandidatesAsync`, so a **title** match ranks a post above body-only matches of a common
  term. SQL-verified: the crew post went from absent-in-top-12 to rank #1.

**Deferred retrieval-quality ideas (noted, not built):** verbose/vague queries still dilute FTS with
common terms (rare-term/IDF weighting could help); the recency (Phase 2) + Preferred-tier boosts can
over-favor recent general announcements over a specific older post. Revisit if a real case recurs
that title-weighting doesn't already cover.

**Why (live incident, 2026-07-24):** even after retrieval was fixed (Phase 2 + the forum-title
fix) and the complexity router disabled so the strong model answered, Hoshi still cited a *wrong*
immortality-crew (Annorax/E-Data + Chang + S31 Georgiou; the authoritative forum post is Old Mudd /
Ro Mudd / Eurydice) and repeated a confabulated date ("24. Juli 2026") verbatim across attempts.
Root cause found in `GuildMemories`: the memory-consolidation extractor had distilled **speculative
user chatter** ("angeblich unsterblich", "Geheimtipp von Anor") and earlier confabulated exchanges
into confident, declarative "fact" memories (rows Id 5/8/9), which then **outweighed the correctly
retrieved authoritative post** in the prompt. This is the memory-layer analogue of the index
self-citation bug (`1628feb`) — a self-reinforcing hallucination loop — but subtler: the memory job
already skips the bot's own messages (`MemoryConsolidationJob.cs:116`), so it's not the same guard.
Two distinct gaps:

1. **Extraction over-commitment** — `MemoryExtractor` turns speculation, questions, and unverified
   claims from chat into declarative facts. Factual game-mechanics/build claims especially should
   not be manufactured from chat distillation; those belong to authoritative knowledge channels.
2. **Grounding precedence** — a chat-derived episodic memory can override an authoritative retrieved
   knowledge source in the prompt; here the model weighted a memory above the actual post.

**Shape (design during implementation):**

- **Extraction discipline** — tighten the `MemoryExtractor` prompt to only capture what actually
  happened/was stated, not speculation/questions/unverified claims; skip distilling factual
  game-mechanic/build/how-to claims entirely (reserve memory for social/community-flavored episodic
  context, which is its actual purpose per `docs/ai-chat-member-lore.md`). Possibly drop
  interrogative/hedged source lines ("angeblich", "?", "gibt es …?") before extraction.
- **Precedence in the prompt** — in `AiChatService.BuildSystemInstructionAsync`, make authoritative
  blocks (retrieved knowledge sources, structured DB facts) explicitly outrank the episodic-memory
  block, and instruct the model that memories are soft recollections that must yield to (or be
  omitted when they conflict with) a retrieved source. Consider not injecting episodic memories at
  all for factual/how-to questions where the knowledge index is the right authority.
- **Confabulation containment** — optionally dampen or skip forming a memory that contradicts the
  knowledge index, and/or gate factual-sounding memories on corroboration from an authoritative
  source.

**Open questions:** how to cheaply classify "authoritative vs speculative" at extraction time;
whether factual memories should exist at all vs only social/episodic ones; whether to add a one-off
cleanup for any already-formed confabulated memories (the test guild's were hand-deleted
2026-07-24).

**Verification:** reproduce with a speculative-question conversation and confirm no confident
factual memory forms; confirm that when a memory conflicts with a retrieved authoritative post, the
post wins in the answer (re-run the immortality-crew question → cites Old Mudd / Ro Mudd / Eurydice).

## Phase 4 — Embedding-provider degradation signaling

**Why:** incident #3 wasn't just invisible to the operator — it was invisible to the *bot's own
behavior*, too: FTS-only degradation is silent and "graceful" by design (per
`docs/ai-chat-rag.md`), which is correct, but a guild stuck there for a day deserves a visible
signal somewhere, not just quietly worse answers.

**Shape:** lean on Phase 1's health data rather than new silent auto-behavior — surface a warning
banner directly on the guild's `AiChatEditor.razor` settings page (not just the separate health
tab) when embedding coverage has been degraded for more than some threshold (e.g. >1 hour of
consecutive failures). Deliberately **not** proposing an automatic fallback to Ollama embeddings
on repeated Gemini failure in this phase — matches this codebase's existing bias toward visible,
human-confirmed changes over silent automatic ones (see the member-messaging-opt-in precedent);
an auto-fallback could be a later opt-in toggle once the visibility exists to know if it's even
needed.

## Phase 5 — ANN vector index (HNSW)

**Why:** already flagged in `docs/backlog.md` as deferred ("v1 does a sequential cosine scan...
add an index if a guild's index grows large"). The test guild is already at ~39k indexed rows —
a concrete data point that "large" is worth revisiting, not just a hypothetical.

**Shape:** standard pgvector `CREATE INDEX ... USING hnsw` migration once row-count crosses a
chosen threshold; benchmark query latency before/after on the current sequential-scan baseline to
confirm it's worth the write-side cost (HNSW build/maintenance overhead). Lowest-risk phase — pure
performance, no behavior change.

## Phase 6 — Chunking (larger structural change, do last)

**Why:** flagged as a deliberate "v1 compromise" in `docs/ai-chat-rag.md` — retrieval unit is
still one whole Discord message (capped at 4000 chars). Incident #1's original over-synthesis bug
was partly enabled by this: a single long patch-notes message listing three parallel, independent
"sources of Critical Mitigation" as one retrieval unit gave the model more surface area to
conflate facts than if each bullet were its own retrievable chunk.

**Shape:** deliberately under-specified here — this is the highest-effort, highest-risk phase
(needs a chunk-to-parent-message reference, re-chunking on edit, chunk-level dedup in ranking) and
should get its own dedicated design pass once Phases 1–5 land, informed by real data from Phase 1's
observability (e.g., "how often does a single indexed message actually contain multiple
independent facts that get conflated?").

## Explicitly lower priority

Not part of this roadmap's phases, kept only as a pointer so they aren't lost:

- **Image/vision support** — already scoped in `docs/backlog.md` ("AI chat — image/vision
  support"); the recommended next step there (audit knowledge channels for image-only content
  before building) still stands, just deprioritized behind the reliability phases above.
- **Member-lore Phase 2 remainder** — introductions-channel seeding and passive post-analysis
  suggestions from `docs/ai-chat-member-lore.md` are the only pieces of that doc's plan not yet
  built (Phase 1, 1.5, and the DM-interview core of Phase 2 are already shipped — that doc's
  header still says "idea / design notes, not yet built," which is stale and worth correcting
  separately from this roadmap).
