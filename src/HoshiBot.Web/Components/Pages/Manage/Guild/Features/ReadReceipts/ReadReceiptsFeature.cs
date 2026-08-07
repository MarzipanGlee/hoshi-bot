using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.ReadReceipts;

public class ReadReceiptsFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ReadReceipts;
    public string Slug => "read-receipts";

    public string Icon => "oi-check";
    public Type EditorComponentType => typeof(ReadReceiptsEditor);

    // Enabled with no kind switched on tracks nothing, which is indistinguishable from off — so at
    // least one implemented kind has to be chosen before this counts as configured.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        foreach (var kind in ReadablePostKinds.Implemented)
        {
            if (string.Equals(await context.GetTextAsync(guildId, Feature, audience, guildAllianceId, ReadReceiptsSettingKeys.ForKind(kind)), "true", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
