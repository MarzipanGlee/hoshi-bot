namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Scheduled-event advance warnings (AllianceTournamentNotifyJob /
    // InfiniteIncursionsNotifyJob).
    public static class Event
    {
        public static string TournamentScheduled(Language lang, long start) =>
            MessageCatalog.Format(lang, "Event.TournamentScheduled", ("start", start));

        public static string IncursionsScheduled(Language lang, string region, long start) =>
            MessageCatalog.Format(lang, "Event.IncursionsScheduled", ("region", region), ("start", start));
    }
}
