using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Discovery half of the permission audit: every channel this guild has pointed a feature at, with
// what the bot does there. The five storage shapes a channel id can live in are unchanged — what
// changed is that the profile now comes from GuildFeaturePermissions instead of an inline switch
// per query.
//
// That difference is the whole point. The old version decided per *feature*, which cannot express
// reality: AnnouncementForwarder reads its GuildFeatureChannel rows and writes to its Channel
// setting, so auditing it as one thing told correctly-configured guilds they had a problem. It also
// guessed which settings were channels by `Key.EndsWith("Channel")`; now a key is a channel exactly
// when a feature declares a slot for it.
//
// In Data rather than Web because it is pure EF, and because HoshiBot.Discord will want the same
// resolution when reporting a permission failure at runtime.
public class BotChannelRequirementService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    // Deliberately NOT filtered by feature enablement — a channel configured for a feature that is
    // currently switched off is still worth reporting, which is what the page has always done.
    // Callers that care about enablement (the per-feature badges) pass enabled scopes to
    // ChannelAccessEvaluator.GroupByFeature instead.
    public async Task<IReadOnlyList<ChannelAccessRequirement>> LoadAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var requirements = new List<ChannelAccessRequirement>();

        // 1. GuildFeatureChannel lists (AI chat listen + knowledge tiers, client releases,
        //    announcement-forwarder sources). Audience-scoped, never per-alliance.
        var featureChannels = await db.GuildFeatureChannels
            .Where(c => c.GuildId == guildId)
            .Select(c => new { c.Feature, c.Audience, c.ChannelId })
            .ToListAsync(cancellationToken);
        foreach (var row in featureChannels)
        {
            foreach (var slot in Slots(row.Feature, ChannelSlotSource.FeatureChannelList))
                requirements.Add(new ChannelAccessRequirement(
                    row.Feature, slot.Source, slot.Key, row.Audience, null, row.ChannelId, slot.Profile, slot.CategoryExpands));
        }

        // 2. Alert channels, matched back to the feature that declared their Kind.
        var alertChannels = await db.GuildAlertChannels
            .Where(c => c.GuildId == guildId)
            .Select(c => new { c.Kind, c.Audience, c.ChannelId })
            .ToListAsync(cancellationToken);
        foreach (var row in alertChannels)
        {
            if (Find(ChannelSlotSource.AlertChannel, row.Kind.ToString()) is not { } match)
                continue;
            requirements.Add(new ChannelAccessRequirement(
                match.Feature, match.Slot.Source, match.Slot.Key, row.Audience, null, row.ChannelId, match.Slot.Profile, match.Slot.CategoryExpands));
        }

        // 3. Per-feature settings. A key is a channel exactly when some feature declares a Setting
        //    slot for it — no more inferring it from the key's name.
        var settings = await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId)
            .Select(s => new { s.Feature, s.Audience, s.GuildAllianceId, s.Key, s.Value })
            .ToListAsync(cancellationToken);
        foreach (var row in settings)
        {
            var slot = Slots(row.Feature, ChannelSlotSource.Setting).FirstOrDefault(s => s.Key == row.Key);
            if (slot.Profile == ChannelAccessProfile.None)
                continue;
            requirements.Add(new ChannelAccessRequirement(
                row.Feature, slot.Source, slot.Key, row.Audience, row.GuildAllianceId, row.Value, slot.Profile, slot.CategoryExpands));
        }

        // 4. The guild-wide slots: the activity log, and the admin channel every feature's failure
        //    reports fall back to. Feature is null — they belong to the guild, not to a feature.
        //    (StfcNews also declares the admin channel; both rows are kept so the page can say who
        //    needs it, and the Fix path unions them per channel anyway.)
        var guildSettings = await db.GuildSettings.AsNoTracking()
            .Where(s => s.GuildId == guildId)
            .Select(s => new { s.LogChannelId, s.AdminChannelId })
            .FirstOrDefaultAsync(cancellationToken);
        if (guildSettings is not null)
        {
            foreach (var slot in GuildFeaturePermissions.GuildWideSlots)
            {
                var channelId = slot.Key == nameof(GuildChannelColumn.Log) ? guildSettings.LogChannelId : guildSettings.AdminChannelId;
                Add(null, slot, GuildAudience.Guild, null, channelId);
            }

            foreach (var (feature, slot) in GuildFeaturePermissions.AllSlots.Where(s => s.Slot.Source == ChannelSlotSource.GuildChannel))
            {
                var channelId = slot.Key == nameof(GuildChannelColumn.Log) ? guildSettings.LogChannelId : guildSettings.AdminChannelId;
                Add(feature, slot, GuildAudience.Guild, null, channelId);
            }
        }

        // 5. The Command Bridge hub columns, per linked alliance. The other seven channel columns on
        //    GuildAlliance have no bot consumer at all and are no longer audited — nothing posts to
        //    them, so demanding permissions there was pure noise (see docs/backlog.md).
        var alliances = await db.GuildAlliances
            .Where(a => a.GuildId == guildId)
            .Select(a => new { a.Id, a.CommandBridgeChannelId, a.StaffCommandBridgeChannelId, a.FriendsCommandBridgeChannelId })
            .ToListAsync(cancellationToken);
        foreach (var alliance in alliances)
        {
            foreach (var (feature, slot) in GuildFeaturePermissions.AllSlots.Where(s => s.Slot.Source == ChannelSlotSource.AllianceColumn))
            {
                var channelId = slot.Key switch
                {
                    nameof(AllianceChannelColumn.CommandBridge) => alliance.CommandBridgeChannelId,
                    nameof(AllianceChannelColumn.StaffCommandBridge) => alliance.StaffCommandBridgeChannelId,
                    _ => alliance.FriendsCommandBridgeChannelId,
                };
                Add(feature, slot, GuildAudience.Alliance, alliance.Id, channelId);
            }
        }

        return requirements;

        void Add(GuildFeature? feature, FeatureChannelSlot slot, GuildAudience audience, int? allianceId, ulong? channelId)
        {
            if (channelId is not { } id || id == 0)
                return;
            requirements.Add(new ChannelAccessRequirement(feature, slot.Source, slot.Key, audience, allianceId, id, slot.Profile, slot.CategoryExpands));
        }
    }

    private static IEnumerable<FeatureChannelSlot> Slots(GuildFeature feature, ChannelSlotSource source) =>
        GuildFeaturePermissions.ChannelSlots(feature).Where(s => s.Source == source);

    private static (GuildFeature Feature, FeatureChannelSlot Slot)? Find(ChannelSlotSource source, string key) =>
        GuildFeaturePermissions.AllSlots.FirstOrDefault(s => s.Slot.Source == source && s.Slot.Key == key) is { Slot.Profile: not ChannelAccessProfile.None } match
            ? match
            : null;
}
