namespace HoshiBot.Domain.Entities;

// Confirmed against legacy's reaction-handler.yag:47-63 — 4 reactions (🟩/🟨/🟥/🟦), the
// last (Direct) sharing the same "Normal" alarm-level text as 🟩 but with the Bot color
// and no "im Auftrag von {role}" attribution (a direct bot announcement, not on staff's
// behalf) — a real 4th tier, not folded into Normal.
public enum AnnouncementSeverity
{
    Normal,
    Elevated,
    High,
    Direct,
}
