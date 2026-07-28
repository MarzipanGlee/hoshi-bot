namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Game-client release announcements (StfcClientReleaseNotifyJob). Platform display
    // names and store names ("Google Play Store", "Apple App Store") are proper nouns and
    // stay code-side arguments.
    public static class Client
    {
        public static string NewVersionTitle(Language lang, string platform) =>
            MessageCatalog.Format(lang, "Client.NewVersionTitle", ("platform", platform));

        public static string Released(Language lang, string version) =>
            MessageCatalog.Format(lang, "Client.Released", ("version", version));

        public static string ReleasedOnStore(Language lang, string version, string store) =>
            MessageCatalog.Format(lang, "Client.ReleasedOnStore", ("version", version), ("store", store));
    }
}
