using HoshiBot.Data;
using HoshiBot.Domain;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord;

// Discord caps both select-menu options and slash-command choices at 25, and a plain
// choices list can't dynamically filter — so a static list can't hold all systems with
// station housing (554 as of the last sync). Autocomplete has no such cap on the
// candidate set: it searches on every keystroke and only the returned suggestion list
// is capped at 25, which is exactly what's needed here.
public class StationHousingSystemAutocompleteProvider(HoshiBotDbContext db)
    : IAutocompleteProvider<AutocompleteInteractionContext>
{
    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(
        ApplicationCommandInteractionDataOption option, AutocompleteInteractionContext context)
    {
        var rawQuery = option.Value ?? "";

        // Cyrillic input can't be matched with a SQL LIKE against the English names we
        // store — fall back to an in-memory phonetic-key substring match (see
        // AlertService.FindSystemByNameAsync for the same fallback on the modal path).
        // 554 candidates is cheap to scan per keystroke.
        if (CyrillicTransliterator.ContainsCyrillic(rawQuery))
        {
            var key = SystemNamePhoneticKey.Compute(rawQuery);
            var housingSystems = await db.StfcSystems.Where(s => s.HasStationHousing).ToListAsync();
            var phoneticMatches = housingSystems
                .Where(s => key.Length == 0 || SystemNamePhoneticKey.Compute(s.Name).Contains(key))
                .OrderBy(s => s.Name)
                .Take(25);

            return phoneticMatches.Select(s => new ApplicationCommandOptionChoiceProperties(s.Name, s.Name));
        }

        var query = rawQuery.ToUpper();

        // ToUpper() on both sides: EF Core's default LIKE translation for .Contains() is
        // case-insensitive on SQLite (ASCII only) but case-sensitive on Npgsql — this
        // makes it consistent across both providers rather than relying on SQLite's
        // incidental behavior.
        var matches = await db.StfcSystems
            .Where(s => s.HasStationHousing && s.Name.ToUpper().Contains(query))
            .OrderBy(s => s.Name)
            .Take(25)
            .ToListAsync();

        return matches.Select(s => new ApplicationCommandOptionChoiceProperties(s.Name, s.Name));
    }
}
