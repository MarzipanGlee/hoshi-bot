using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

// The decision half of the permission audit had no tests at all before it was pulled out of
// PermissionAuditService — it was tangled up with RestClient/IMemoryCache/IDbContextFactory and
// couldn't have any. These are the rules that were only ever verified by looking at the page.
public class ChannelAccessEvaluatorTests
{
    private const ulong Channel = 100;
    private const ulong Other = 200;

    private static ChannelAccessRequirement Req(
        GuildFeature? feature, ChannelAccessProfile profile, ulong channelId = Channel, int? allianceId = null) =>
        new(feature, ChannelSlotSource.Setting, "Channel", GuildAudience.Guild, allianceId, channelId, profile);

    private static Func<ulong, BotPermission?> Perms(params (ulong Channel, BotPermission Effective)[] map) =>
        id => map.Where(m => m.Channel == id).Select(m => (BotPermission?)m.Effective).FirstOrDefault();

    [Fact]
    public void Missing_is_the_difference_between_required_and_effective()
    {
        var findings = ChannelAccessEvaluator.Evaluate(
            [Req(GuildFeature.Absences, ChannelAccessProfile.Post)],
            Perms((Channel, BotPermission.ViewChannel | BotPermission.SendMessages)));

        Assert.Equal(BotPermission.EmbedLinks, findings[0].Missing);
        Assert.False(findings[0].Ok);
    }

    // The reason Ok isn't simply "Missing == None": a channel that isn't in the guild's live list
    // has no effective permissions to subtract, so the naive version reads as healthy.
    [Fact]
    public void A_channel_that_no_longer_exists_is_missing_everything_not_nothing()
    {
        var findings = ChannelAccessEvaluator.Evaluate(
            [Req(GuildFeature.Absences, ChannelAccessProfile.Post)],
            Perms());

        Assert.False(findings[0].ChannelExists);
        Assert.False(findings[0].Ok);
        Assert.Equal(ChannelAccessProfile.Post.Permissions(), findings[0].Missing);
    }

    [Fact]
    public void Extra_permissions_the_bot_happens_to_hold_are_not_a_problem()
    {
        var findings = ChannelAccessEvaluator.Evaluate(
            [Req(GuildFeature.Absences, ChannelAccessProfile.Read)],
            Perms((Channel, BotPermission.ViewChannel | BotPermission.ReadMessageHistory | BotPermission.ManageRoles)));

        Assert.True(findings[0].Ok);
        Assert.Equal(BotPermission.None, findings[0].Missing);
    }

    // The whole point of splitting the forwarder's sources from its destination: a read-only slot
    // must not demand Send Messages, or a correctly configured guild is told it has a problem.
    [Fact]
    public void A_read_only_slot_does_not_demand_send_or_embed()
    {
        var required = ChannelAccessProfile.Read.Permissions();

        Assert.Equal(BotPermission.None, required & BotPermission.SendMessages);
        Assert.Equal(BotPermission.None, required & BotPermission.EmbedLinks);
    }

    // A private thread on a text channel is created with CreatePrivateThreads; the bot then posts
    // inside the thread, never in the parent channel, so SendMessages is not required.
    [Fact]
    public void Private_threads_do_not_demand_send_on_the_parent()
    {
        var required = ChannelAccessProfile.PrivateThreads.Permissions();

        Assert.Equal(BotPermission.None, required & BotPermission.SendMessages);
        Assert.Equal(BotPermission.CreatePrivateThreads, required & BotPermission.CreatePrivateThreads);
        Assert.Equal(BotPermission.SendMessagesInThreads, required & BotPermission.SendMessagesInThreads);
    }

    // Forums are the exception, and getting it wrong is what this test exists for: creating a forum
    // post is SendMessages ("Create Posts" in the forum's own permission UI), NOT
    // CreatePublicThreads — which Discord doesn't even offer on a forum channel. Demanding the
    // latter sent an admin looking for a permission that isn't there while ignoring the one they
    // had already granted.
    [Fact]
    public void Creating_a_forum_post_needs_send_messages_not_create_public_threads()
    {
        var required = ChannelAccessProfile.ForumPosts.Permissions();

        Assert.Equal(BotPermission.SendMessages, required & BotPermission.SendMessages);
        Assert.Equal(BotPermission.SendMessagesInThreads, required & BotPermission.SendMessagesInThreads);
        Assert.False(Enum.IsDefined(typeof(BotPermission), "CreatePublicThreads"),
            "CreatePublicThreads is unused — nothing creates a public thread on a text channel. "
            + "If that changes, re-add it AND check no forum slot picked it up by mistake.");
    }

    // Two features sharing one channel: the Fix button has to grant the union, or fixing from one
    // row leaves the other failing on the very next re-check.
    [Fact]
    public void RequiredByChannel_unions_every_feature_pointing_at_the_same_channel()
    {
        var findings = ChannelAccessEvaluator.Evaluate(
        [
            Req(GuildFeature.Announcements, ChannelAccessProfile.Draft),
            Req(GuildFeature.AiChat, ChannelAccessProfile.Reply),
            Req(GuildFeature.Absences, ChannelAccessProfile.Post, Other),
        ], Perms());

        var union = ChannelAccessEvaluator.RequiredByChannel(findings)[Channel];

        Assert.Equal(BotPermission.AddReactions, union & BotPermission.AddReactions);   // from Draft
        Assert.Equal(BotPermission.EmbedLinks, union & BotPermission.EmbedLinks);       // from Draft
        Assert.Equal(BotPermission.ReadMessageHistory, union & BotPermission.ReadMessageHistory);
        Assert.Equal(ChannelAccessProfile.Post.Permissions(), ChannelAccessEvaluator.RequiredByChannel(findings)[Other]);
    }

    [Fact]
    public void A_guild_level_permission_the_bot_lacks_marks_the_feature_missing()
    {
        var summaries = ChannelAccessEvaluator.GroupByFeature(
            [], BotPermission.ViewChannel,
            [new FeatureScope(GuildFeature.NicknameSync, GuildAudience.Guild, null)]);

        var nickname = Assert.Single(summaries);
        Assert.Equal(FeaturePermissionStatus.Missing, nickname.Status);
        Assert.Equal(BotPermission.ManageNicknames, nickname.MissingGuildPermissions);
    }

    // A role-sync feature has no channels at all — it must read Ok once its guild permission is
    // granted, not "nothing configured".
    [Fact]
    public void A_feature_with_no_channel_slots_is_ok_once_its_guild_permission_is_granted()
    {
        var summaries = ChannelAccessEvaluator.GroupByFeature(
            [], BotPermission.ManageRoles,
            [new FeatureScope(GuildFeature.ConditionalRoles, GuildAudience.Guild, null)]);

        Assert.Equal(FeaturePermissionStatus.Ok, Assert.Single(summaries).Status);
    }

    // …whereas a feature that DOES declare slots and has configured none is a different state, so
    // the UI can stay quiet about it (IsConfiguredAsync already says that).
    [Fact]
    public void An_enabled_feature_with_slots_but_nothing_configured_reports_NoChannelsConfigured()
    {
        var summaries = ChannelAccessEvaluator.GroupByFeature(
            [], BotPermission.None,
            [new FeatureScope(GuildFeature.Tickets, GuildAudience.Guild, null)]);

        Assert.Equal(FeaturePermissionStatus.NoChannelsConfigured, Assert.Single(summaries).Status);
    }

    // The knowledge tiers are storage-only pseudo-features with no feature card of their own.
    [Fact]
    public void Knowledge_tier_findings_group_under_AiChat()
    {
        var findings = ChannelAccessEvaluator.Evaluate(
        [
            Req(GuildFeature.AiChatKnowledge, ChannelAccessProfile.Read),
            Req(GuildFeature.AiChatKnowledgePreferred, ChannelAccessProfile.Read, Other),
        ], Perms((Channel, ChannelAccessProfile.Read.Permissions()), (Other, ChannelAccessProfile.Read.Permissions())));

        var summary = Assert.Single(ChannelAccessEvaluator.GroupByFeature(findings, BotPermission.None, []));

        Assert.Equal(GuildFeature.AiChat, summary.Feature);
        Assert.Equal(2, summary.Findings.Count);
    }

    // Per-alliance settings are independent: one alliance being misconfigured must not turn the
    // other one red. (Absences also needs ManageRoles guild-wide for its notification-role sync,
    // so that has to be granted here or BOTH scopes are legitimately red for the same reason.)
    [Fact]
    public void Alliance_scopes_are_summarised_separately()
    {
        var findings = ChannelAccessEvaluator.Evaluate(
        [
            Req(GuildFeature.Absences, ChannelAccessProfile.Post, Channel, allianceId: 1),
            Req(GuildFeature.Absences, ChannelAccessProfile.Post, Other, allianceId: 2),
        ], Perms((Channel, ChannelAccessProfile.Post.Permissions()), (Other, BotPermission.ViewChannel)));

        var summaries = ChannelAccessEvaluator.GroupByFeature(findings, BotPermission.ManageRoles, []);

        Assert.Equal(FeaturePermissionStatus.Ok, summaries.Single(s => s.GuildAllianceId == 1).Status);
        Assert.Equal(FeaturePermissionStatus.Missing, summaries.Single(s => s.GuildAllianceId == 2).Status);
    }

    [Fact]
    public void Guild_wide_slots_carry_no_feature_and_are_left_out_of_the_feature_groups()
    {
        var findings = ChannelAccessEvaluator.Evaluate([Req(null, ChannelAccessProfile.Post)], Perms());

        Assert.Empty(ChannelAccessEvaluator.GroupByFeature(findings, BotPermission.None, []));
    }

    [Fact]
    public void Problems_sort_ahead_of_healthy_features()
    {
        var findings = ChannelAccessEvaluator.Evaluate(
        [
            Req(GuildFeature.Absences, ChannelAccessProfile.Post),
            Req(GuildFeature.Tickets, ChannelAccessProfile.PrivateThreads, Other),
        ], Perms((Channel, ChannelAccessProfile.Post.Permissions()), (Other, BotPermission.ViewChannel)));

        var summaries = ChannelAccessEvaluator.GroupByFeature(findings, BotPermission.ManageRoles, []);

        Assert.Equal(GuildFeature.Tickets, summaries[0].Feature);
        Assert.Equal(FeaturePermissionStatus.Ok, summaries[1].Status);
    }
}
