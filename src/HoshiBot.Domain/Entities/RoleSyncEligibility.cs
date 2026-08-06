namespace HoshiBot.Domain.Entities;

// Whether a role/nickname change would be accepted by Discord, decided BEFORE the call is made.
//
// The point is not tidiness. Discord bans an IP that returns 10,000 invalid responses (401/403/429)
// in any 10 minutes, and the role-sync jobs catch Forbidden inside their per-member loops — so one
// role sitting above the bot's is one 403 per member, per job, per run, forever. Its own rate-limit
// guide asks for exactly this: "403 responses are avoided by inspecting role or channel permissions
// and by not making requests that are restricted by such permissions."
//
// Only guild-level facts live here, deliberately. Channel permissions need overwrite resolution and
// category inheritance, which is genuinely easy to get wrong; role positions and the guild-level
// permission bits are not. See CONTRIBUTING "Discord API limits".
public static class RoleSyncEligibility
{
    public static bool CanManageRoles(BotPermission botGuildPermissions) =>
        botGuildPermissions.HasFlag(BotPermission.Administrator) || botGuildPermissions.HasFlag(BotPermission.ManageRoles);

    public static bool CanManageNicknames(BotPermission botGuildPermissions) =>
        botGuildPermissions.HasFlag(BotPermission.Administrator) || botGuildPermissions.HasFlag(BotPermission.ManageNicknames);

    // Hierarchy, which is a SEPARATE rule from the permission bits and catches people out:
    // Administrator does not bypass it. A bot can only touch roles strictly below its own highest
    // role, so an equal position means no — when two roles share a position Discord breaks the tie
    // by id, and guessing that is not worth a 403 per member.
    public static bool CanAssign(int botHighestRolePosition, int targetRolePosition) =>
        targetRolePosition < botHighestRolePosition;

    // Renaming has the same hierarchy rule plus one absolute: nobody can rename the guild owner, not
    // even with Administrator.
    public static bool CanRename(int botHighestRolePosition, int memberHighestRolePosition, bool memberIsOwner) =>
        !memberIsOwner && memberHighestRolePosition < botHighestRolePosition;
}
