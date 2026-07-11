namespace HoshiBot.Domain.Entities;

// STFC's Ops Level tiers, bucketed from the numeric 1-80 Ops Level stat — unlike
// StfcPlayerRank, these buckets don't correspond 1:1 to a raw feed value, so
// StfcPlayer stores the raw level (see StfcPlayer.OpsLevel) and this is derived from it
// on demand via FromLevel wherever the group is needed (role sync, display).
public enum StfcOpsGroup
{
    G1 = 1,
    G2 = 2,
    G3 = 3,
    G4 = 4,
    G5 = 5,
    G6 = 6,
    G7 = 7,
}

public static class StfcOpsGroupExtensions
{
    public static StfcOpsGroup? FromLevel(int? level) => level switch
    {
        >= 1 and <= 9 => StfcOpsGroup.G1,
        >= 10 and <= 19 => StfcOpsGroup.G2,
        >= 20 and <= 39 => StfcOpsGroup.G3,
        >= 40 and <= 50 => StfcOpsGroup.G4,
        >= 51 and <= 60 => StfcOpsGroup.G5,
        >= 61 and <= 70 => StfcOpsGroup.G6,
        >= 71 and <= 80 => StfcOpsGroup.G7,
        _ => null,
    };
}
