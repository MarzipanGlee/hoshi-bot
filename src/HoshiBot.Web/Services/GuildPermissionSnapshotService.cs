using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Web.Services;

// Per-(feature, audience, alliance) permission status for a whole guild in one object, so the
// Features index can badge 32 cards without asking Discord 32 times — or even once more than it
// already does. The three Discord reads it needs (channels, roles, bot member) are already
// 60s-cached per guild by DiscordGuildDataService and already loaded by the pages that render the
// badges; everything after that is in-memory flag math. The only new I/O is one
// BotChannelRequirementService.LoadAsync, which sits naturally beside FeatureSettingsSnapshot's
// own bulk load.
//
// Deliberately contains NO localized strings — only enums and flags. It goes into the shared
// IMemoryCache, whose entries cross circuits and users, and a cached German label would leak into
// an English admin's page (the rule stated on PermissionAuditService).
public sealed record GuildPermissionSnapshot(
    IReadOnlyDictionary<FeatureScope, FeaturePermissionStatus> ByScope,
    BotPermission BotGuildPermissions,
    bool DiscordUnavailable)
{
    public static readonly GuildPermissionSnapshot Unavailable =
        new(new Dictionary<FeatureScope, FeaturePermissionStatus>(), BotPermission.None, DiscordUnavailable: true);

    // Exact-scope lookup. A guild-wide roll-up would be wrong on an alliance page: one alliance
    // misconfigured would show red on another alliance whose own setup is fine.
    public FeaturePermissionStatus For(GuildFeature feature, GuildAudience audience, int? guildAllianceId) =>
        ByScope.GetValueOrDefault(
            new FeatureScope(GuildFeaturePermissions.DisplayOwner(feature), audience, guildAllianceId),
            DiscordUnavailable ? FeaturePermissionStatus.Unknown : FeaturePermissionStatus.Ok);
}

public sealed class GuildPermissionSnapshotService(
    DiscordGuildDataService discordData,
    BotChannelRequirementService requirementService,
    GuildFeatureService featureService,
    IMemoryCache cache,
    IConfiguration configuration)
{
    public const string CacheKeyPrefix = "bot-permission-report:";

    public async Task<GuildPermissionSnapshot> GetAsync(ulong guildId)
    {
        var cached = await cache.GetOrCreateAsync($"{CacheKeyPrefix}{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await BuildAsync(guildId);
        });

        return cached ?? GuildPermissionSnapshot.Unavailable;
    }

    private async Task<GuildPermissionSnapshot> BuildAsync(ulong guildId)
    {
        try
        {
            var channels = await discordData.GetChannelsAsync(guildId);
            var botUserId = ulong.Parse(configuration["Discord:ClientId"]!);
            var status = await discordData.GetBotRoleStatusAsync(guildId, botUserId);

            var requirements = ExpandCategories(await requirementService.LoadAsync(guildId), channels);
            var findings = ChannelAccessEvaluator.Evaluate(requirements, channelId =>
                channels.FirstOrDefault(c => c.Id == channelId) is { } channel
                    ? (status.BotMember?.GetChannelPermissions(status.BotPermissions, channel) ?? status.BotPermissions).ToDomain()
                    : null);

            var scopes = (await featureService.GetEnabledAsync(guildId))
                .Select(e => new FeatureScope(e.Feature, e.Audience, e.GuildAllianceId))
                .ToList();

            var byScope = ChannelAccessEvaluator
                .GroupByFeature(findings, status.BotPermissions.ToDomain(), scopes)
                .ToDictionary(s => new FeatureScope(s.Feature, s.Audience, s.GuildAllianceId), s => s.Status);

            return new GuildPermissionSnapshot(byScope, status.BotPermissions.ToDomain(), DiscordUnavailable: false);
        }
        catch (RestException)
        {
            // A badge built on nothing must not read green — For() returns Unknown instead.
            return GuildPermissionSnapshot.Unavailable;
        }
    }

    // Same expansion the audit page does: a configured knowledge entry may be a category, and the
    // bot works on its child channels. Duplicated deliberately rather than shared with
    // PermissionAuditService — that one works from a PermissionAuditContext (which carries a
    // Language for its error strings) and this must stay language-free to be cacheable.
    private static List<ChannelAccessRequirement> ExpandCategories(
        IReadOnlyList<ChannelAccessRequirement> requirements, IReadOnlyList<IGuildChannel> channels)
    {
        var expanded = new List<ChannelAccessRequirement>();
        foreach (var requirement in requirements)
        {
            if (!requirement.CategoryExpands
                || channels.FirstOrDefault(c => c.Id == requirement.ChannelId) is not CategoryGuildChannel category)
            {
                expanded.Add(requirement);
                continue;
            }

            var children = channels
                .Where(c => DiscordGuildDataService.GetParentCategoryId(c) == category.Id && c is TextGuildChannel or ForumGuildChannel)
                .Select(c => requirement with { ChannelId = c.Id, ViaCategoryId = category.Id })
                .ToList();

            expanded.AddRange(children.Count > 0 ? children : [requirement]);
        }

        return expanded;
    }
}
