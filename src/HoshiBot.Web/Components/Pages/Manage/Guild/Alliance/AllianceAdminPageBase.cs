using HoshiBot.Data;
using HoshiBot.Web.Services;
using Microsoft.AspNetCore.Components;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Alliance;

// Shared plumbing for the per-alliance admin pages (Alliance Settings, Command Bridge): resolve
// which linked alliance the page targets and canonicalize the URL to /alliance/{id}/{slug}.
// Mirrors FeatureSettings.razor's alliance-resolution precedence (validated route id → top-bar
// selection → primary link) so all alliance-scoped pages behave identically, and the top-bar
// AllianceSelector's /alliance/{id}/... rewrite lands on a real page.
public abstract class AllianceAdminPageBase : GuildAdminPageBase
{
    [Parameter] public int? GuildAllianceIdRaw { get; set; }

    [Inject] protected GuildAllianceService AllianceService { get; set; } = null!;
    [Inject] protected CurrentAllianceContext AllianceContext { get; set; } = null!;

    // The resolved link this page is editing; null while NeedsAllianceLink is true.
    protected int? ResolvedAllianceId { get; private set; }

    // True when the guild has no alliance linked at all — the page shows a "link one first" hint.
    protected bool NeedsAllianceLink { get; private set; }

    // The path segment after /alliance/{id}/ this page lives at (e.g. "settings").
    protected abstract string PageSlug { get; }

    protected sealed override async Task OnAuthorizedParametersSetAsync()
    {
        NeedsAllianceLink = false;
        ResolvedAllianceId = null;

        var id = await ResolveAllianceIdAsync(GuildAllianceIdRaw);
        if (id is null)
        {
            NeedsAllianceLink = true;
            return;
        }

        // Canonicalize so the URL is deep-linkable and the AllianceSelector's rewrite works.
        if (GuildAllianceIdRaw != id)
        {
            Nav.NavigateTo($"manage/guild/{GuildId}/alliance/{id}/{PageSlug}", replace: true);
            return;
        }

        ResolvedAllianceId = id;
        await OnAllianceResolvedAsync(id.Value);
    }

    // Subclass hook — runs once the target alliance is known and the URL is canonical.
    protected virtual Task OnAllianceResolvedAsync(int guildAllianceId) => Task.CompletedTask;

    private async Task<int?> ResolveAllianceIdAsync(int? routeId)
    {
        if (routeId is { } id && await AllianceService.FindByIdAsync(ParsedGuildId, id) is not null)
            return id;
        if (AllianceContext.GuildAllianceId is { } selected && await AllianceService.FindByIdAsync(ParsedGuildId, selected) is not null)
            return selected;
        return await AllianceService.GetPrimaryIdAsync(ParsedGuildId);
    }
}
