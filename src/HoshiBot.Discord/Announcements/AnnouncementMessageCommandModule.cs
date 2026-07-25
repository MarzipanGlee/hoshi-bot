using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.Announcements;

// MessageCommandContext is a sibling of ApplicationCommandContext (both just implement
// IApplicationCommandContext), not a subtype — needs its own module base, confirmed via
// reflection against the installed NetCord package before writing this.
public class AnnouncementMessageCommandModule(GuildFeatureService featureService, GuildFeatureSettingsService settingsService, EmbedBranding embedBranding)
    : ApplicationCommandModule<MessageCommandContext>
{
    [MessageCommand("Vorschau erstellen")]
    public async Task<InteractionMessageProperties> Preview()
    {
        var guildId = Context.Guild!.Id;
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.Announcements))
            return await embedBranding.EphemeralAsync(guildId, GuildFeatureService.DisabledMessage(GuildFeature.Announcements));

        var draft = Context.Target;

        // Unambiguous once an admin has split each audience's draft channel apart (the
        // common case, including every guild that never splits — it only ever has one
        // draft channel to begin with). Right after this guild's migration, before any
        // splitting, a channel can still match 2+ audiences (all pointing at the same
        // legacy channel) — ask explicitly rather than guessing.
        // Phase 1 stays audience-based (the specific alliance is resolved at publish time as the
        // primary link). Collapse the scopes to distinct audiences: one → straight to severity.
        var scopes = await settingsService.FindScopesByValueAsync(guildId, GuildFeature.Announcements, AnnouncementsSettingKeys.DraftChannel, draft.ChannelId);
        var audiences = scopes.Select(s => s.Audience).Distinct().ToList();
        return audiences.Count == 1
            ? AnnouncementButtonModule.BuildSeverityPrompt(draft, audiences[0])
            : AnnouncementButtonModule.BuildAudiencePrompt(draft);
    }
}
