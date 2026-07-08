namespace HoshiBot.Domain.Entities;

// Latest known up/down/maintenance state for a real STFC server. One row per server
// (keyed by StfcServerId itself, not a separate surrogate Id, since this is a 1:1
// extension of StfcServer rather than a many-per-parent child collection).
//
// Status/Maintenance is what's currently observed; NotifiedStatus/NotifiedMaintenance is
// what was last actually announced to Discord — kept separate so a notify job can just
// diff the two instead of the data source needing to know anything about notifications.
// Currently populated by a one-time seed (see StfcServerStatusSeedData) rather than a
// recurring automated sync — stfc.pro's robots.txt disallows /api/ for automated agents,
// so this is temporary until api.stfc.pro (an official, permitted endpoint) is ready.
public class StfcServerStatus
{
    public int StfcServerId { get; set; }

    public StfcServer StfcServer { get; set; } = null!;

    public int Status { get; set; }

    public required string Maintenance { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int? NotifiedStatus { get; set; }

    public string? NotifiedMaintenance { get; set; }
}
