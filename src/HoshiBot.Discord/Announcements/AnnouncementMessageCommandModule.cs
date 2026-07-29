using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.Announcements;

// MessageCommandContext is a sibling of ApplicationCommandContext (both just implement
// IApplicationCommandContext), not a subtype — needs its own module base, confirmed via
// reflection against the installed NetCord package before writing this. It also needs its own
// application-command service: Host Program.cs registers a dedicated
// AddApplicationCommands<MessageCommandInteraction, MessageCommandContext>() — the default
// ApplicationCommandContext service never picks this module up (verified against the package:
// without the extra service the bot registered only the 12 slash commands and this command
// silently didn't exist on Discord).
public class AnnouncementMessageCommandModule(GuildFeatureService featureService, GuildFeatureSettingsService settingsService, EmbedBranding embedBranding,
    LanguageResolver languageResolver)
    : ApplicationCommandModule<MessageCommandContext>
{
    // Canonical name is English; the German name ("Vorschau erstellen") is a localization in
    // HoshiBot.Host/Localizations/de.json — the ONE place a command NAME is localized, because a
    // context-menu entry's name is its only visible text (there is no description line under it).
    [MessageCommand("Create preview")]
    public async Task<InteractionMessageProperties> Preview()
    {
        var guildId = Context.Guild!.Id;

        // Everything this returns (disabled guard, audience/severity prompts) is an ephemeral
        // wizard message for the invoking staff member — their language.
        var lang = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, guildId);
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.Announcements))
            return await embedBranding.EphemeralAsync(guildId, GuildFeatureService.DisabledMessage(GuildFeature.Announcements, lang));

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
            ? AnnouncementButtonModule.BuildSeverityPrompt(draft, audiences[0], lang)
            : AnnouncementButtonModule.BuildAudiencePrompt(draft, lang);
    }
}
