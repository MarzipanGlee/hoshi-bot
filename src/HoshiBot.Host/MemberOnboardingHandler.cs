using HoshiBot.Data;
using HoshiBot.Discord;
using HoshiBot.Discord.MemberOnboarding;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace HoshiBot.Host;

// Real-time player-assignment onboarding. Fires on BOTH a member joining (GUILD_MEMBER_ADD) and any
// member change (GUILD_MEMBER_UPDATE) — the latter is what catches the common case where a member
// only receives the alliance member role after a verification step, well after the raw join. Either
// way, the moment a member holds the member role we run the PlayerLink matcher: a confident single
// in-alliance nickname match links them silently; anything ambiguous becomes an Unresolved
// PlayerLinkReview for the admin table AND — if the opt-in MemberOnboarding campaign is active — gets
// an immediate confirmation DM instead of waiting for the periodic MemberOnboardingSyncJob. The
// periodic job stays as the backstop for members already present before the feature was enabled.
//
// Requires the GuildUsers (GUILD_MEMBERS) privileged intent — already enabled in Program.cs.
// Auto-registered by AddGatewayHandlers(typeof(Program).Assembly).
public class MemberOnboardingHandler(IServiceScopeFactory scopeFactory, ILogger<MemberOnboardingHandler> logger)
    : IGuildUserAddGatewayHandler, IGuildUserUpdateGatewayHandler
{
    public ValueTask HandleAsync(GuildUser user) => OnboardAsync(user);

    private async ValueTask OnboardAsync(GuildUser user)
    {
        if (user.IsBot)
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();
            var featureService = scope.ServiceProvider.GetRequiredService<GuildFeatureService>();
            var settingsService = scope.ServiceProvider.GetRequiredService<GuildFeatureSettingsService>();
            var playerLinkService = scope.ServiceProvider.GetRequiredService<PlayerLinkService>();
            var onboarding = scope.ServiceProvider.GetRequiredService<MemberOnboardingService>();

            await EnsureGuildMemberAsync(db, user.GuildId, user.Id);

            var linkIds = await featureService.GetEnabledAllianceIdsAsync(user.GuildId, GuildFeature.PlayerLink);
            foreach (var linkId in linkIds)
            {
                var link = await db.GuildAlliances.FirstOrDefaultAsync(ga => ga.Id == linkId);
                var memberRole = await settingsService.GetSnowflakeAsync(user.GuildId, GuildFeature.PlayerLink, GuildAudience.Alliance, linkId, PlayerLinkSettingKeys.MemberRole)
                    ?? link?.MemberRoleId;
                if (memberRole is not { } memberRoleId || !user.RoleIds.Contains(memberRoleId))
                    continue;

                var outcome = await playerLinkService.ProcessMemberAsync(user.GuildId, linkId, user.Id, CommanderName.Of(user));

                // Ambiguous/unmatched → if MemberOnboarding is on and its campaign is active, DM the
                // member now rather than waiting for the paced job. SendOutreachAsync flips the row to
                // DmSent, so the periodic job won't double-DM.
                if (outcome == PlayerLinkOutcome.Queued
                    && await featureService.IsEnabledAsync(user.GuildId, GuildFeature.MemberOnboarding, GuildAudience.Alliance, linkId)
                    && await IsCampaignActiveAsync(settingsService, user.GuildId, linkId))
                {
                    var review = await db.PlayerLinkReviews.FirstOrDefaultAsync(
                        r => r.GuildId == user.GuildId && r.DiscordUserId == user.Id && r.Status == PlayerLinkReviewStatus.Unresolved);
                    if (review is not null)
                        await onboarding.SendOutreachAsync(review, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PlayerLink onboarding failed for user {UserId} in guild {GuildId}", user.Id, user.GuildId);
        }
    }

    private static async Task<bool> IsCampaignActiveAsync(GuildFeatureSettingsService settings, ulong guildId, int linkId) =>
        string.Equals(
            await settings.GetTextAsync(guildId, GuildFeature.MemberOnboarding, GuildAudience.Alliance, linkId, MemberOnboardingSettingKeys.CampaignActive),
            "true", StringComparison.OrdinalIgnoreCase);

    private static async Task EnsureGuildMemberAsync(HoshiBotDbContext db, ulong guildId, ulong userId)
    {
        var added = false;
        if (await db.DiscordUsers.FindAsync(userId) is null)
        {
            db.DiscordUsers.Add(new DiscordUser { DiscordUserId = userId });
            added = true;
        }
        if (await db.GuildMembers.FindAsync(guildId, userId) is null)
        {
            db.GuildMembers.Add(new GuildMember { GuildId = guildId, DiscordUserId = userId, JoinedAt = DateTimeOffset.UtcNow });
            added = true;
        }
        if (added)
            await db.SaveChangesAsync();
    }
}
