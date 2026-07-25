using HoshiBot.Data;
using HoshiBot.Domain.Entities;
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
    GuildFeatureService featureService,
    EmbedBranding embedBranding)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("staff-shield-report")]
    public Task<InteractionMessageProperties> ReportManual() => ShieldReportPromptAsync(ShieldLossVariant.Manual);

    [ComponentInteraction("staff-shield-incursions")]
    public Task<InteractionMessageProperties> ReportIncursions() => ShieldReportPromptAsync(ShieldLossVariant.InfiniteIncursions);

    [ComponentInteraction("staff-shield-territory-reset")]
    public Task<InteractionMessageProperties> ReportTerritoryReset() => ShieldReportPromptAsync(ShieldLossVariant.TerritoryReset);

    private async Task<InteractionMessageProperties> ShieldReportPromptAsync(ShieldLossVariant variant)
    {
        if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.ShieldReminders) is { } msg)
            return await EphemeralEmbedAsync(msg);

        return await EphemeralEmbedAsync(
            "Wähle den Commander, dessen Schildverlust gemeldet werden soll.",
            [new UserMenuProperties($"staff-shield-target:{variant}")],
            title: "Schildverlust melden");
    }

    [ComponentInteraction("staff-shield-mute")]
    public async Task<InteractionMessageProperties> MutePrompt()
    {
        if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.ShieldReminders) is { } msg)
            return await EphemeralEmbedAsync(msg);

        return await EphemeralEmbedAsync(
            "Wähle den Commander, dessen Stummschaltung der öffentlichen Schildablaufwarnungen geändert werden soll.",
            [new UserMenuProperties("staff-shield-mute-target")],
            title: "Öffentliche Schildablaufwarnungen verwalten");
    }

    // Enable/disable buttons live on this module's own ephemeral wizard message (posted by the
    // mute user-menu step), so ModifyMessage is safe.
    [ComponentInteraction("staff-shield-mute-set")]
    public Task SetMute(ulong targetUserId, string action) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var muted = action == "on";
            var result = await alertService.SetShieldMutedAsync(Context.Guild!.Id, targetUserId, muted);
            var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, result, title: "Öffentliche Schildablaufwarnungen verwalten");
            return m => { m.Embeds = [embed]; m.Components = []; };
        });

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
