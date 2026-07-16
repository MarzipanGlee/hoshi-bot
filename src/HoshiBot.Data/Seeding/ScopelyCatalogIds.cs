namespace HoshiBot.Data.Seeding;

// Scopely's own region/veil-group numbering — assigned explicitly rather than derived, since
// it's a fixed set of 3/6 values that will essentially never grow. Shared by the initial
// catalog seeder (SeedStfcCatalogIfEmptyAsync) and StfcCatalogImportService (the admin-panel
// import that keeps the catalog current after that first seed) so both assign the same stable
// IDs to the same region/veil-group name — a single source of truth instead of two copies that
// could drift.
public static class ScopelyCatalogIds
{
    public static readonly IReadOnlyDictionary<string, int> Regions = new Dictionary<string, int>
    {
        ["US"] = 1,
        ["EU"] = 2,
        ["APAC"] = 3,
    };

    public static readonly IReadOnlyDictionary<string, int> VeilGroups = new Dictionary<string, int>
    {
        ["US-1"] = 1,
        ["US-2"] = 2,
        ["US-3"] = 3,
        ["EU-4"] = 4,
        ["EU-5"] = 5,
        ["APAC-6"] = 6,
    };
}
