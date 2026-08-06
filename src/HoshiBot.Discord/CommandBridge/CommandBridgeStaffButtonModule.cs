using HoshiBot.Data;
using HoshiBot.Discord.Alerts;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

// Button handlers for the staff bridge ("Kommandobrücke Führungsstab"). These live in a
// staff-only channel (the bridge is posted there), which is the access gate — matching the
// legacy bot. Each shield action re-checks the ShieldReminders feature, since Discord can
// deliver a click from a stale hub message after the feature was disabled.
public class CommandBridgeStaffButtonModule(
    AlertService alertService,
    HoshiBotDbContext db,
    PlayerLinkService playerLinkService,
    GuildFeatureService featureService,
    EmbedBranding embedBranding,
    LanguageResolver languageResolver)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    // Every step of these staff wizards is ephemeral to the clicking staff member — their
    // language. (The member the report is about hears from AlertService later, separately.)
    private Task<Language> ActingUserLanguageAsync() =>
        languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

    [ComponentInteraction("staff-shield-report")]
    public Task<InteractionMessageProperties> ReportManual() => ShieldReportPromptAsync(ShieldLossVariant.Manual);

    [ComponentInteraction("staff-shield-incursions")]
    public Task<InteractionMessageProperties> ReportIncursions() => ShieldReportPromptAsync(ShieldLossVariant.InfiniteIncursions);

    [ComponentInteraction("staff-shield-territory-reset")]
    public Task<InteractionMessageProperties> ReportTerritoryReset() => ShieldReportPromptAsync(ShieldLossVariant.TerritoryReset);

    private async Task<InteractionMessageProperties> ShieldReportPromptAsync(ShieldLossVariant variant)
    {
        var lang = await ActingUserLanguageAsync();
        if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.ShieldReminders, lang) is { } msg)
            return await EphemeralEmbedAsync(msg);

        return await EphemeralEmbedAsync(
            Msg.Bridge.StaffShieldTargetPrompt(lang),
            [new UserMenuProperties($"staff-shield-target:{variant}")],
            title: Msg.Bridge.StaffShieldTitle(lang));
    }

    [ComponentInteraction("staff-shield-mute")]
    public async Task<InteractionMessageProperties> MutePrompt()
    {
        var lang = await ActingUserLanguageAsync();
        if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.ShieldReminders, lang) is { } msg)
            return await EphemeralEmbedAsync(msg);

        return await EphemeralEmbedAsync(
            Msg.Bridge.StaffMuteTargetPrompt(lang),
            [new UserMenuProperties("staff-shield-mute-target")],
            title: Msg.Bridge.StaffMuteTitle(lang));
    }

    // Enable/disable buttons live on this module's own ephemeral wizard message (posted by the
    // mute user-menu step), so ModifyMessage is safe.
    [ComponentInteraction("staff-shield-mute-set")]
    public Task SetMute(ulong targetUserId, string action) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var lang = await ActingUserLanguageAsync();
            var muted = action == "on";
            var result = await alertService.SetShieldMutedAsync(Context.Guild!.Id, targetUserId, muted, lang);
            var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, result, title: Msg.Bridge.StaffMuteTitle(lang));
            return m => { m.Embeds = [embed]; m.Components = []; };
        });

    // Who is in the alliance in-game but has no account here — the gap staff actually chase.
    // Answered from the player links, so it needs Player Assignment: without those links every
    // player would look missing.
    //
    // Deferred rather than answered inline: it reads the whole roster, and an interaction that
    // takes longer than ~3 seconds is dead (CONTRIBUTING "Known gotchas").
    [ComponentInteraction("staff-missing-players")]
    public Task MissingPlayers() =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var lang = await ActingUserLanguageAsync();
            if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.PlayerLink, lang) is { } msg)
                return msg;

            // The staff bridge is posted per linked alliance, so the channel the click came from
            // says which alliance is being asked about. A click from anywhere else (an old message
            // moved, a shared channel) falls back to every linked alliance rather than guessing.
            var stfcAllianceId = await db.GuildAlliances
                .Where(a => a.GuildId == Context.Guild!.Id && a.StaffCommandBridgeChannelId == Context.Channel.Id)
                .Select(a => (int?)a.StfcAllianceId)
                .FirstOrDefaultAsync();

            var alliances = await playerLinkService.GetPlayersMissingFromDiscordAsync(Context.Guild!.Id, stfcAllianceId);
            if (alliances.Count == 0)
                return Msg.Bridge.MissingPlayersNoAlliance(lang);

            var sections = alliances.Select(a => a.MissingNames.Count == 0
                ? Msg.Bridge.MissingPlayersNone(lang, a.AllianceTag)
                : $"{Msg.Bridge.MissingPlayersIntro(lang, a.AllianceTag, a.MissingNames.Count, a.TotalPlayers)}\n{NameList(a.MissingNames, lang)}");

            return string.Join("\n\n", sections);
        });

    // A markdown list, which embeds render properly — one name per line beats a comma soup at this
    // length. An alliance caps at 105 players, so a single alliance always fits; the trim only ever
    // matters for a coalition guild asking about all of them at once.
    private static string NameList(IReadOnlyList<string> names, Language lang)
    {
        const int MaxNames = 100;
        var shown = names.Take(MaxNames).Select(n => $"- {n}");
        return names.Count <= MaxNames
            ? string.Join("\n", shown)
            : string.Join("\n", shown) + "\n" + Msg.Bridge.MissingPlayersMore(lang, names.Count - MaxNames);
    }

    private async Task<InteractionMessageProperties> EphemeralEmbedAsync(string description, IReadOnlyList<IMessageComponentProperties>? components = null, string? title = null)
    {
        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, description, title: title);
        return new InteractionMessageProperties
        {
            Embeds = [embed],
            Flags = MessageFlags.Ephemeral,
            Components = components,
        };
    }
}
