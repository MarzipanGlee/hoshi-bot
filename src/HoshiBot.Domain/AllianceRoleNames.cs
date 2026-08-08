namespace HoshiBot.Domain;

// Default names for the roles a feature owns and offers to create, where the name depends on the
// alliance rather than being a fixed word.
//
// Shared because the notification role is ONE stored setting reachable from three editors — Absences
// owns it, and the Announcements and Territory Capture editors offer the same value under their own
// wording. Three literals would eventually disagree, and disagreeing here does not read as a typo:
// it silently creates a second role, so half the guild is opted into a role nothing pings.
public static class AllianceRoleNames
{
    // "[LF]-Notifications". The tag is what members recognise, and prefixing it keeps each linked
    // alliance's opt-in role adjacent in a coalition guild's role list.
    public static string Notifications(string allianceTag) => $"{allianceTag}-Notifications";
}
