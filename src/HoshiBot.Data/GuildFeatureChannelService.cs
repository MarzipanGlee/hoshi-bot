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
}
