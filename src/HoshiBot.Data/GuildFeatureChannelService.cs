using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// CRUD for GuildFeatureChannel — a feature's list of post-to channels per audience, the
// channel-only counterpart to GuildAlertChannel. Backs the Web MultiChannelPicker and the
// ClientRelease notify job. Deduped on add (the DB has a unique index too, this just avoids the
// exception for the expected "already there" case).
public class GuildFeatureChannelService(IDbContextFactory<HoshiBotDbContext> dbFactory, GuildFeatureService featureService)
{
    public async Task<List<ulong>> GetChannelsAsync(ulong guildId, GuildFeature feature, GuildAudience audience)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildFeatureChannels
            .Where(c => c.GuildId == guildId && c.Feature == feature && c.Audience == audience)
            .Select(c => c.ChannelId)
            .ToListAsync();
    }

    // Every channel configured for this feature across the audiences it's currently enabled for —
    // the notify-job counterpart to GetChannelsAsync, gated the same way
    // NotificationDispatcher.SendPublicToEnabledAudiencesAsync gates GuildAlertChannel rows.
    public async Task<List<ulong>> GetEnabledAudienceChannelsAsync(ulong guildId, GuildFeature feature)
    {
        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, feature);
        if (enabledAudiences.Count == 0)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildFeatureChannels
            .Where(c => c.GuildId == guildId && c.Feature == feature && enabledAudiences.Contains(c.Audience))
            .Select(c => c.ChannelId)
            .Distinct()
            .ToListAsync();
    }

    // Which ENABLED audiences have this specific channel registered. GetEnabledAudienceChannelsAsync
    // flattens the same data into a distinct channel list, which answers "is this channel watched"
    // but loses "by whom" — and a feature with a per-audience destination has to know whose channel
    // it just saw a message in.
    public async Task<List<GuildAudience>> GetAudiencesForChannelAsync(ulong guildId, GuildFeature feature, ulong channelId)
    {
        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, feature);
        if (enabledAudiences.Count == 0)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.GuildFeatureChannels
            .Where(c => c.GuildId == guildId && c.Feature == feature && c.ChannelId == channelId)
            .Select(c => c.Audience)
            .Distinct()
            .ToListAsync())
            .Where(enabledAudiences.Contains)
            .ToList();
    }

    public async Task AddAsync(ulong guildId, GuildFeature feature, GuildAudience audience, ulong channelId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var exists = await db.GuildFeatureChannels.AnyAsync(c =>
            c.GuildId == guildId && c.Feature == feature && c.Audience == audience && c.ChannelId == channelId);
        if (exists)
            return;

        db.GuildFeatureChannels.Add(new GuildFeatureChannel
        {
            GuildId = guildId,
            Feature = feature,
            Audience = audience,
            ChannelId = channelId,
        });
        await db.SaveChangesAsync();
    }

    public async Task RemoveAsync(ulong guildId, GuildFeature feature, GuildAudience audience, ulong channelId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.GuildFeatureChannels
            .Where(c => c.GuildId == guildId && c.Feature == feature && c.Audience == audience && c.ChannelId == channelId)
            .ExecuteDeleteAsync();
    }

    // Adds a channel to one feature bucket while ensuring it's in ONLY that bucket among a set of
    // mutually-exclusive sibling features (same guild + audience) — e.g. the three AI-chat
    // knowledge-priority tiers, where a channel belongs to exactly one tier. Removes it from the
    // siblings first, then adds (both idempotent), so re-assigning a channel to another tier "moves"
    // it rather than duplicating it.
    public async Task AddExclusiveAsync(ulong guildId, GuildFeature feature, GuildAudience audience, ulong channelId, IReadOnlyList<GuildFeature> exclusiveWith)
    {
        foreach (var sibling in exclusiveWith)
            await RemoveAsync(guildId, sibling, audience, channelId);
        await AddAsync(guildId, feature, audience, channelId);
    }
}
