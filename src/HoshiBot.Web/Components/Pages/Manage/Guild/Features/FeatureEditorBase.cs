using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features;

// Shared load/save/enable-toggle plumbing for the per-feature editors — each editor still
// owns its own specific fields/markup (that's the point of having 12 bespoke editors
// instead of one generic reflection-driven one), this only removes the identical
// boilerplate around them: fetch-or-create GuildSettings, toggle via GuildFeatureService,
// persist one field at a time (autosave, no batch Save button).
public abstract class FeatureEditorBase : ComponentBase
{
    // The viewer's language, cascaded by the layout — every editor renders catalog text
    // (at minimum the shared Discord create-error line) with it.
    [CascadingParameter]
    public Language Lang { get; set; }

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

    [Inject]
    protected GuildFeatureSettingsService SettingsService { get; set; } = null!;

    protected abstract GuildFeature Feature { get; }

    // The concrete audience this editor acts on: the explicit parameter for multi-audience
    // features, or the feature's one relevant audience otherwise.
    protected GuildAudience ResolvedAudience => Audience ?? GuildFeatureAudiences.SingleAudience(Feature);

    protected bool Enabled { get; private set; }
    protected GuildSettings Settings { get; private set; } = new();

    // This scope's alliance tag, or empty for a scope that has none. Loaded here rather than by each
    // editor that wants it: prefixing is what every role this bot creates for an alliance does, and
    // several editors were offering to create a bare "Alerts"/"Diplomat"/"Commodore" purely because
    // the prefix was per-page work they had not done. See TagPrefixed.
    protected string AllianceTag { get; private set; } = "";

    protected override async Task OnInitializedAsync()
    {
        Enabled = await FeatureService.IsEnabledAsync(GuildId, Feature, ResolvedAudience, GuildAllianceId);

        await using var db = await DbFactory.CreateDbContextAsync();
        Settings = await db.GuildSettings.AsNoTracking().FirstOrDefaultAsync(s => s.GuildId == GuildId)
            ?? new GuildSettings { GuildId = GuildId };

        if (GuildAllianceId is { } allianceId)
        {
            AllianceTag = await db.GuildAlliances.AsNoTracking()
                .Where(a => a.Id == allianceId && a.GuildId == GuildId)
                .Select(a => a.StfcAlliance.Tag)
                .FirstOrDefaultAsync() ?? "";
        }

        await OnSettingsLoadedAsync();
    }

    // The name a create-role option offers: "LF-Alerts" for an alliance scope, bare "Alerts" where
    // there is no tag to prefix with. Keeps a coalition guild's per-alliance roles distinguishable in
    // a role list that would otherwise hold five identically-named ones.
    protected string TagPrefixed(string name) =>
        string.IsNullOrWhiteSpace(AllianceTag) ? name : $"{AllianceTag}-{name}";

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

    // Per-setting read/write for this editor's (GuildId, Feature, ResolvedAudience, GuildAllianceId)
    // scope — thin wrappers over GuildFeatureSettingsService so editors pass only the key. The
    // overloads taking an explicit feature serve the few settings a page shares with another
    // feature's editor at the same audience/alliance scope (e.g. Territory Capture editing the
    // Absences-owned notification role); a setting stored under a *different* audience scope
    // (ClientRelease's guild-wide platform roles, Announcements' Alliance-only role) still calls
    // SettingsService directly with its deliberate explicit scope.
    protected Task<ulong?> GetSnowflakeAsync(string key) => GetSnowflakeAsync(Feature, key);

    protected Task<ulong?> GetSnowflakeAsync(GuildFeature feature, string key) =>
        SettingsService.GetSnowflakeAsync(GuildId, feature, ResolvedAudience, GuildAllianceId, key);

    protected Task SetSnowflakeAsync(string key, ulong? value) => SetSnowflakeAsync(Feature, key, value);

    protected Task SetSnowflakeAsync(GuildFeature feature, string key, ulong? value) =>
        SettingsService.SetSnowflakeAsync(GuildId, feature, ResolvedAudience, GuildAllianceId, key, value);

    protected Task<List<ulong>> GetSnowflakeListAsync(string key) =>
        SettingsService.GetSnowflakeListAsync(GuildId, Feature, ResolvedAudience, GuildAllianceId, key);

    protected Task AddSnowflakeListValueAsync(string key, ulong value) =>
        SettingsService.AddSnowflakeListValueAsync(GuildId, Feature, ResolvedAudience, GuildAllianceId, key, value);

    protected Task RemoveSnowflakeListValueAsync(string key, ulong value) =>
        SettingsService.RemoveSnowflakeListValueAsync(GuildId, Feature, ResolvedAudience, GuildAllianceId, key, value);

    protected Task<string?> GetTextAsync(string key) =>
        SettingsService.GetTextAsync(GuildId, Feature, ResolvedAudience, GuildAllianceId, key);

    protected Task SetTextAsync(string key, string? value) =>
        SettingsService.SetTextAsync(GuildId, Feature, ResolvedAudience, GuildAllianceId, key, value);

    protected Task SetSecretAsync(string key, string? value) =>
        SettingsService.SetSecretAsync(GuildId, Feature, ResolvedAudience, GuildAllianceId, key, value);
}
