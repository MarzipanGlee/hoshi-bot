namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Alliance diplomacy slash command (AllianceModule): /set-diplomacy.
    public static class Alliance
    {
        public static string NotManagedHere(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Alliance.NotManagedHere", ("tag", tag));

        public static string TargetNotFound(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Alliance.TargetNotFound", ("tag", tag));

        // status: the DiplomacyStatus enum name as the user picked it from the slash-command
        // choices — those choice names are command metadata (sub-phase 6g), so the echo here
        // deliberately stays the raw enum name.
        public static string DiplomacySet(Language lang, string source, string name, string tag, string status) =>
            MessageCatalog.Format(lang, "Alliance.DiplomacySet",
                ("source", source), ("name", name), ("tag", tag), ("status", status));
    }
}
