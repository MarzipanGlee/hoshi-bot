using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The NotificationDispatcher's own admin-facing texts: the throttled permission-issue embed
    // and the activity-log entry for skipped (undeliverable) channel sends.
    //
    // What the bot was TRYING to do now lives here too, as one enum-keyed family rather than a
    // per-feature Action* key each caller passed in as free text — the value is also the throttle
    // key, and a localized string made a poor one.
    public static class Notify
    {
        // "Notify.Action.<BotAction>". Enum-keyed, so there is no compile check — a missing key
        // would render the raw enum name rather than leaking a catalog key, and
        // BotActionCatalogTests asserts every value has one in both locales.
        public static string Action(Language lang, BotAction action)
        {
            var key = $"Notify.Action.{action}";
            var label = MessageCatalog.Format(lang, key);
            return label == key ? action.ToString() : label;
        }




        public static string PermissionIssueChannel(Language lang, string action, string channel, string permissions) =>
            MessageCatalog.Format(lang, "Notify.PermissionIssueChannel", ("action", action), ("channel", channel), ("permissions", permissions));

        // For the guild-wide failures (assigning a role, renaming a member) — there is no channel
        // to name, and no channel override that would fix them.
        public static string PermissionIssueGuild(Language lang, string action, string permissions) =>
            MessageCatalog.Format(lang, "Notify.PermissionIssueGuild", ("action", action), ("permissions", permissions));

        public static string PermissionIssueHelp(Language lang) =>
            MessageCatalog.Format(lang, "Notify.PermissionIssueHelp");
    }
}
