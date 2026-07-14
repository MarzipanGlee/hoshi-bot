using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features;

// Shared load/save/enable-toggle plumbing for the per-feature editors — each editor still
// owns its own specific fields/markup (that's the point of having 12 bespoke editors
// instead of one generic reflection-driven one), this only removes the identical
// boilerplate around them: fetch-or-create GuildSettings, toggle via GuildFeatureService,
// persist one field at a time (autosave, no batch Save button).
public abstract class FeatureEditorBase : ComponentBase
{
    [Parameter, EditorRequired]
    public ulong GuildId { get; set; }

    // Which audience/alliance scope this editor instance configures. Supplied by the route
    // shell (FeatureSettings.razor). Audience is null for single-audience features (resolved
    // from GuildFeatureAudiences); GuildAllianceId is set exactly for the Alliance audience
    // (the selected linked alliance) and null otherwise — see ResolvedAudience/FeatureScopeGuard.
    [Parameter]
    public GuildAudience? Audience { get; set; }

    [Parameter]
    public int? GuildAllianceId { get; set; }

    [Inject]
    protected IDbContextFactory<HoshiBotDbContext> DbFactory { get; set; } = null!;

    [Inject]
    protected GuildFeatureService FeatureService { get; set; } = null!;

    protected abstract GuildFeature Feature { get; }

    // The concrete audience this editor acts on: the explicit parameter for multi-audience
    // features, or the feature's one relevant audience otherwise.
    protected GuildAudience ResolvedAudience => Audience ?? GuildFeatureAudiences.SingleAudience(Feature);

    protected bool Enabled { get; private set; }
    protected GuildSettings Settings { get; private set; } = new();

    protected override async Task OnInitializedAsync()
    {
        Enabled = await FeatureService.IsEnabledAsync(GuildId, Feature, ResolvedAudience, GuildAllianceId);

        await using var db = await DbFactory.CreateDbContextAsync();
        Settings = await db.GuildSettings.AsNoTracking().FirstOrDefaultAsync(s => s.GuildId == GuildId)
            ?? new GuildSettings { GuildId = GuildId };

        await OnSettingsLoadedAsync();
    }

    // Hook for a subclass to populate its own local *Input string fields from Settings
    // after the base load completes — string-bound since <select>/ChannelPicker/RolePicker
    // bind string values, not raw ulong?.
    protected virtual Task OnSettingsLoadedAsync() => Task.CompletedTask;

    protected async Task ToggleEnabledAsync(bool enabled)
    {
        await FeatureService.SetEnabledAsync(GuildId, Feature, ResolvedAudience, GuildAllianceId, enabled);
        Enabled = enabled;
    }

    protected async Task SaveAsync(Action<GuildSettings> apply)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var existing = await db.GuildSettings.FirstOrDefaultAsync(s => s.GuildId == GuildId);
        if (existing is null)
        {
            existing = new GuildSettings { GuildId = GuildId };
            db.GuildSettings.Add(existing);
        }

        apply(existing);
        await db.SaveChangesAsync();
    }

    protected static ulong? ParseId(string? input) => ulong.TryParse(input, out var id) ? id : null;
}
