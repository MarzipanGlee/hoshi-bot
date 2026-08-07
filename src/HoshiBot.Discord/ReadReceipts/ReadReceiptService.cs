using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.ReadReceipts;

// Everything read confirmation needs, so the features that post things don't have to know any of it:
// they call RegisterAsync after posting and, if it says yes, hang Buttons() under their message.
//
// Which kinds carry a button is an admin setting (GuildFeature.ReadReceipts), but the answer is
// resolved ONCE — at RegisterAsync — and stored on the row. Every later read comes from
// ReadablePost.ReadReceiptsEnabled, never from the setting. That is what makes a setting change
// affect only new posts: a member who was asked to confirm something is never left holding a button
// that has quietly stopped counting, and turning a kind on never retroactively demands confirmation
// of posts nobody was asked about.
public class ReadReceiptService(HoshiBotDbContext db, GatewayClient gatewayClient, GuildFeatureService featureService, GuildFeatureSettingsService settingsService)
{
    // Registers a freshly posted message and answers whether it should carry the button. Returns the
    // row so the caller can build the button with its id.
    public async Task<ReadablePost> RegisterAsync(ulong guildId, ulong channelId, ulong messageId, ReadablePostKind kind,
        GuildAudience audience, int? guildAllianceId, string title, Language lang, CancellationToken cancellationToken = default)
    {
        var enabled = await IsKindEnabledAsync(guildId, kind, audience, guildAllianceId);

        var post = new ReadablePost
        {
            GuildId = guildId,
            ChannelId = channelId,
            MessageId = messageId,
            Kind = kind,
            Audience = audience,
            GuildAllianceId = guildAllianceId,
            ReadReceiptsEnabled = enabled,
            Title = Clamp(title),
            Language = lang,
            PostedAt = DateTimeOffset.UtcNow,
        };

        db.ReadablePosts.Add(post);
        await db.SaveChangesAsync(cancellationToken);
        return post;
    }

    // The live setting, consulted only when a post is created — in the scope that post belongs to,
    // so an alliance can ask for confirmations while the community channel doesn't. Unset means off,
    // so a newly declared kind starts silent.
    public async Task<bool> IsKindEnabledAsync(ulong guildId, ReadablePostKind kind, GuildAudience audience, int? guildAllianceId)
    {
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.ReadReceipts, audience, guildAllianceId))
            return false;

        var value = await settingsService.GetTextAsync(guildId, GuildFeature.ReadReceipts, audience, guildAllianceId, ReadReceiptsSettingKeys.ForKind(kind));
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    // The row under a tracked post: confirm this one, and see whatever is still unconfirmed. The
    // second is the same action, custom id and icon as the Command Bridge's unread button.
    public static ActionRowProperties Buttons(int postId, int readCount, Language lang) => new(
    [
        ReadButton(postId, readCount, lang),
        new ButtonProperties("announcement-show-unread", Msg.Bridge.AnnouncementsUnread(lang), EmojiProperties.Standard(Icons.Unread), ButtonStyle.Primary),
    ]);

    public static ButtonProperties ReadButton(int postId, int readCount, Language lang) =>
        new($"read-receipt:{postId}", Msg.Announce.ReadButton(lang, readCount), EmojiProperties.Standard(Icons.Ok), ButtonStyle.Primary);

    // Titles come from whatever the producer had — an announcement's first line, a forwarded post's
    // source heading — so they are not length-bounded at the source.
    private static string Clamp(string title) =>
        title.Length <= 200 ? title : title[..200];

    public static string Icon(ReadablePostKind kind) => kind switch
    {
        ReadablePostKind.ForwardedAnnouncement => Icons.Translation,
        ReadablePostKind.DiplomacyPost => Icons.PostDiplomacy,
        ReadablePostKind.WelcomeMessage => Icons.PostWelcome,
        ReadablePostKind.AllianceRules => Icons.PostRules,
        _ => Icons.PostAnnouncement,
    };

    // Follows a post whose message had to be re-posted rather than edited (the forwarder self-heals
    // that way when a human deletes its output). Without this the ✅ points at a dead message and the
    // count silently stops moving.
    public async Task MoveAsync(ulong guildId, ulong oldMessageId, ulong newMessageId, CancellationToken cancellationToken = default)
    {
        var post = await db.ReadablePosts.FirstOrDefaultAsync(p => p.GuildId == guildId && p.MessageId == oldMessageId, cancellationToken);
        if (post is null)
            return;

        post.MessageId = newMessageId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReadablePost?> FindAsync(int postId) =>
        await db.ReadablePosts.FirstOrDefaultAsync(p => p.Id == postId);

    public async Task<(bool WasNew, int Count)> MarkReadAsync(int postId, ulong guildId, ulong userId)
    {
        var now = DateTimeOffset.UtcNow;

        if (await db.DiscordUsers.FindAsync(userId) is null)
            db.DiscordUsers.Add(new DiscordUser { DiscordUserId = userId });
        if (await db.GuildMembers.FindAsync(guildId, userId) is null)
            db.GuildMembers.Add(new GuildMember { GuildId = guildId, DiscordUserId = userId, JoinedAt = now });

        var existing = await db.ReadReceipts.FirstOrDefaultAsync(r => r.ReadablePostId == postId && r.DiscordUserId == userId);

        var wasNew = existing is null;
        if (wasNew)
        {
            db.ReadReceipts.Add(new ReadReceipt
            {
                ReadablePostId = postId,
                GuildId = guildId,
                DiscordUserId = userId,
                ReadAt = now,
            });
            await db.SaveChangesAsync();
        }

        var count = await db.ReadReceipts.CountAsync(r => r.ReadablePostId == postId);
        return (wasNew, count);
    }

    // Tracked posts this member hasn't confirmed AND can actually see.
    //
    // The visibility filter is the answer to "was this addressed to me". GuildAudience is documented
    // as UX filtering only and just Alliance has anything member-shaped, so there is no
    // audience-membership model to ask — but a member who can read the channel a post went to is
    // exactly the audience it went to. Without this, a member of one alliance was shown another
    // alliance's announcements.
    //
    // Resolved BEFORE the query so Take(limit) returns the oldest N visible, rather than filtering N
    // down to two afterwards. Costs no REST call: channels and roles come from GUILD_CREATE and are
    // reliably cached, unlike the member cache.
    public async Task<List<ReadablePost>> GetUnreadAsync(ulong guildId, GuildUser member, int limit = 10)
    {
        var channelIds = await db.ReadablePosts
            .Where(p => p.GuildId == guildId && p.ReadReceiptsEnabled)
            .Select(p => p.ChannelId)
            .Distinct()
            .ToListAsync();

        var visible = VisibleChannels(guildId, member, channelIds);

        return await db.ReadablePosts
            .Where(p => p.GuildId == guildId
                && p.ReadReceiptsEnabled
                && visible.Contains(p.ChannelId)
                && !p.Receipts.Any(r => r.DiscordUserId == member.Id))
            .OrderBy(p => p.PostedAt)
            .Take(limit)
            .ToListAsync();
    }

    // Fails OPEN: an uncached guild or channel counts as visible. Hiding something a member should
    // have read is the worse error of the two.
    private List<ulong> VisibleChannels(ulong guildId, GuildUser member, List<ulong> channelIds)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild))
            return channelIds;

        return channelIds
            .Where(id => !guild.Channels.ContainsKey(id)
                // NetCord.Permissions, fully qualified: HoshiBot.Discord.Permissions is a namespace
                // here and shadows the type outright.
                || member.GetChannelPermissions(guild, id).HasFlag(NetCord.Permissions.ViewChannel))
            .ToList();
    }
}
