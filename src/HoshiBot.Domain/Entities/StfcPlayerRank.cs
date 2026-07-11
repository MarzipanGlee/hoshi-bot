namespace HoshiBot.Domain.Entities;

// STFC's fixed 5-tier in-alliance rank (see assets/ranks/ranks.md) — numbered to match the
// external player-data feed's `rankid` field exactly (1-5), so import can cast/validate
// directly with no reindexing.
public enum StfcPlayerRank
{
    Admiral = 1,
    Commodore = 2,
    Premier = 3,
    Operative = 4,
    Agent = 5,
}
