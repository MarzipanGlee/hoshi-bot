namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The Boarding feature's Discord-facing text: the standing message's title, and the two things
    // a member can be told after pressing its button.
    public static class Boarding
    {
        public static string Title(Language lang) =>
            MessageCatalog.Format(lang, "Boarding.Title");

        // Shown once the member role is on. The admin's own message did the welcoming; this is the
        // short ephemeral acknowledgement that it worked.
        public static string Welcome(Language lang) =>
            MessageCatalog.Format(lang, "Boarding.Welcome");

        // The bot could not grant the role — almost always a role sitting above its own. Says the
        // team has been told, because they have: an admin notification goes out at the same time.
        public static string RoleFailed(Language lang) =>
            MessageCatalog.Format(lang, "Boarding.RoleFailed");
    }
}
