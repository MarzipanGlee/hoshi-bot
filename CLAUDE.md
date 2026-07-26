# CLAUDE.md

Guidance for Claude Code when working in this repository.

**Read and follow [CONTRIBUTING.md](CONTRIBUTING.md) first** — it is the authoritative
rulebook (architecture and file placement, language policy, coding conventions, design
rules, known gotchas, testing, workflow). [README.md](README.md) covers setup, secrets,
deployment, and EF migration commands. This file only adds what is specific to working
here as an agent.

## Operational notes

- **Live testing happens in the dedicated test guild**, never the "Lost Falcons"
  production guild (see CONTRIBUTING "Testing"). Note: Lost Falcons' own
  `AdminChannelId` currently has a real Discord-side permission problem (the bot can't
  post there — a pre-existing config issue in that guild, not a code bug), so don't be
  surprised if a notify job logs `Forbidden` for Lost Falcons specifically.
- `deploy.sh`'s migration step always prints `Cannot load library libgssapi_krb5.so.2` —
  harmless Npgsql/Kerberos probe noise; don't chase it or report it as a deploy problem.
- Deferred ideas and "for later" bugs go in [docs/backlog.md](docs/backlog.md), not in
  memory.
- The ongoing refactoring effort lives in
  [docs/refactoring-plan.md](docs/refactoring-plan.md) — check it before introducing a
  pattern it replaces.

## Collaboration notes

- **Get explicit sign-off (screenshot + approval) before committing/pushing any
  visual/styling change to `HoshiBot.Web`** — don't stage and commit right after your
  own local verification passes. A past NotFound-page styling fix looked correct in a
  local screenshot but the user's own test showed it still broken; the deeper issue was
  committing before they got a chance to look at all. Backend logic changes (auth,
  routing, job behavior) already verified via curl/build/test don't need this extra
  gate — this is specific to visual changes.
- **When the user shows concrete real-world evidence (a screenshot of actual
  Discord/browser state) that contradicts a conclusion drawn from reading code, trust
  the evidence and dig deeper — don't just restate the code-based analysis.** Static
  code analysis shows what code currently does, not whether that path was ever actually
  exercised for real. Concretely: this once correctly surfaced a real bug (RoE
  Violations wired to a Forum channel that the current code couldn't actually create
  private threads on).
- **Before "fixing" what looks like an unintended regression in a pre-commit `git
  diff`, ask rather than silently reverting it.** A diff shows *what* changed, not
  *why* or *by whom* — a removal that looks like collateral damage from your own
  earlier edits may be a deliberate change the user made themselves (confirmed
  happening once with an intentionally-removed CSS rule). State the finding and ask
  before restoring it.
