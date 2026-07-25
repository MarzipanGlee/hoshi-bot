namespace HoshiBot.Domain.Attribution;

// A third-party data source the bot's admin surfaces credit ("Powered by {Name}") — the display
// name and site URL the PoweredBy component renders. Which entities come from which source is
// wired in PoweredByRegistry.
public sealed record PoweredBySource(string Name, string Url);

public static class PoweredBySources
{
    public static readonly PoweredBySource TerritoryLol = new("territory.lol", "https://territory.lol/");
    public static readonly PoweredBySource StfcPro = new("stfc.pro", "https://stfc.pro/");
    public static readonly PoweredBySource StfcSpace = new("stfc.space", "https://stfc.space/");
}
