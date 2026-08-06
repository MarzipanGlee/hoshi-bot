using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

// These decide whether a REST call is made at all, so getting one backwards has two very different
// costs: too permissive puts us back to a 403 per member (the ban risk this exists to remove), too
// restrictive silently stops roles syncing, which is worse. Hence tests for the two rules that are
// counter-intuitive rather than just the happy path.
public class RoleSyncEligibilityTests
{
    [Fact]
    public void Manage_roles_is_granted_by_the_bit_or_by_administrator()
    {
        Assert.True(RoleSyncEligibility.CanManageRoles(BotPermission.ManageRoles));
        Assert.True(RoleSyncEligibility.CanManageRoles(BotPermission.Administrator));
        Assert.False(RoleSyncEligibility.CanManageRoles(BotPermission.ViewChannel | BotPermission.SendMessages));
        Assert.False(RoleSyncEligibility.CanManageRoles(BotPermission.None));
    }

    [Fact]
    public void Manage_nicknames_is_granted_by_the_bit_or_by_administrator()
    {
        Assert.True(RoleSyncEligibility.CanManageNicknames(BotPermission.ManageNicknames));
        Assert.True(RoleSyncEligibility.CanManageNicknames(BotPermission.Administrator));
        Assert.False(RoleSyncEligibility.CanManageNicknames(BotPermission.ManageRoles));
    }

    // The rule people expect to be overridable and isn't. An Administrator bot still cannot touch a
    // role above its own — no permission bit appears in CanAssign's signature at all, which is the
    // point.
    [Fact]
    public void Administrator_does_not_bypass_role_hierarchy()
    {
        Assert.False(RoleSyncEligibility.CanAssign(botHighestRolePosition: 5, targetRolePosition: 9));
        Assert.True(RoleSyncEligibility.CanAssign(botHighestRolePosition: 9, targetRolePosition: 5));
    }

    // Equal positions are a real case — Discord breaks the tie by role id, and guessing wrong costs a
    // 403 per member. Refusing is the cheap side of that bet.
    [Fact]
    public void An_equal_role_position_is_not_assignable()
    {
        Assert.False(RoleSyncEligibility.CanAssign(botHighestRolePosition: 7, targetRolePosition: 7));
    }

    [Fact]
    public void The_guild_owner_can_never_be_renamed()
    {
        Assert.False(RoleSyncEligibility.CanRename(botHighestRolePosition: 99, memberHighestRolePosition: 0, memberIsOwner: true));
        Assert.True(RoleSyncEligibility.CanRename(botHighestRolePosition: 99, memberHighestRolePosition: 0, memberIsOwner: false));
    }

    [Fact]
    public void A_member_ranked_at_or_above_the_bot_cannot_be_renamed()
    {
        Assert.False(RoleSyncEligibility.CanRename(botHighestRolePosition: 5, memberHighestRolePosition: 5, memberIsOwner: false));
        Assert.False(RoleSyncEligibility.CanRename(botHighestRolePosition: 5, memberHighestRolePosition: 6, memberIsOwner: false));
        Assert.True(RoleSyncEligibility.CanRename(botHighestRolePosition: 5, memberHighestRolePosition: 4, memberIsOwner: false));
    }
}
