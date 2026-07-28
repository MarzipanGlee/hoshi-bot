namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The thread-removal queue's admin permission notify (ThreadCleanupJob) — generic
    // infrastructure shared by every feature that queues thread deletions, hence its own
    // prefix rather than Ticket./Roe. (whose HintManageThreads texts it deliberately
    // duplicates per-feature).
    public static class Cleanup
    {
        public static string ActionRemoveThread(Language lang) =>
            MessageCatalog.Format(lang, "Cleanup.ActionRemoveThread");

        public static string HintManageThreads(Language lang, string thread) =>
            MessageCatalog.Format(lang, "Cleanup.HintManageThreads", ("thread", thread));
    }
}
