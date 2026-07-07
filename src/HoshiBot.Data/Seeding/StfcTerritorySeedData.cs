namespace HoshiBot.Data.Seeding;

// Territory-capture zone map, hand-transcribed from hoshi-bot-yagpdb's
// Commands/static_data/territories-common.yag (names + neighbours — territory-owners.yag's
// ownership snapshot is NOT used, it's stale) merged with a live current-schedule table the
// user supplied on 2026-07-04 (Weekday/CaptureTimeUtc + current ownership by alliance tag).
// Tier is derived from each zone's *original* legacy per-weekday group
// (territories-common.yag's $daysTier: day1->2, day3/5->1, day4->2 wait no — see below),
// since no better per-zone tier data exists except Qoda (confirmed Tier 4 from an in-game
// info card) — the real schedule has since moved some zones to different weekdays than
// their legacy grouping, but there's no evidence tier itself changed with them.
// The 11 legacy zones that initially had no known current Weekday/CaptureTimeUtc ("not
// captured this cycle") were filled in by the user on 2026-07-05 once observed; those same
// zones still have no ownership row (current owner wasn't part of that follow-up).
// Qoda is a new 56th zone (not in the legacy 55) with a best-effort single neighbour
// ("Framtid", read off the user-supplied map image — flagged as unconfirmed).
// Id is the real Scopely/stfc.pro territory ID (from https://api.stfc.pro/stfc_territories),
// reconciled on 2026-07-05 by cross-referencing several per-server territory-map SVGs against
// that API's per-server ownership tags, then confirmed exactly via a generic map.svg whose
// polygons carry a direct data-id attribute — every one of these 56 zone names matched exactly
// one API territory id, with zero left over on either side.
public static class StfcTerritorySeedData
{
    public static readonly (int Id, string Name, int Tier, DayOfWeek? Weekday, TimeOnly? CaptureTimeUtc, string[] Neighbours)[] Entries =
    [
        // Legacy day-0 (Sunday) group, Tier 1.
        (1834533150, "Tola", 1, DayOfWeek.Sunday, new TimeOnly(8, 0), []),
        (795083356, "Qeyma", 1, DayOfWeek.Monday, new TimeOnly(9, 0), ["Asiti", "Crios", "Kolava"]),
        (1170639956, "Gelida", 1, DayOfWeek.Monday, new TimeOnly(10, 0), ["Beku", "Bimasa", "Saldeti"]),
        (1598736183, "Innlasn", 1, DayOfWeek.Monday, new TimeOnly(11, 0), ["Framtid", "Tefkari", "Vantar"]),
        (1433851648, "Vemira", 1, DayOfWeek.Monday, new TimeOnly(12, 0), ["Hoobishan", "Tezera", "Tigan"]),
        (1238332214, "Helvi", 1, DayOfWeek.Monday, new TimeOnly(13, 0), ["Klefaski", "Lenara", "Zhian"]),
        (763527938, "Aonad", 1, DayOfWeek.Monday, new TimeOnly(14, 0), ["Comst", "Otima", "Ruhe"]),
        (234507462, "Temeri", 1, DayOfWeek.Monday, new TimeOnly(15, 0), ["Adia", "Ezla", "Perim"]),
        (1995161692, "Stilhe", 1, DayOfWeek.Monday, new TimeOnly(16, 0), ["Burran", "Crios", "Roshar"]),
        (599322744, "Parturi", 1, DayOfWeek.Monday, new TimeOnly(17, 0), ["Anzat", "Asiti", "Bolari"]),
        (508588181, "Aylus", 1, DayOfWeek.Wednesday, new TimeOnly(18, 0), ["Duportas", "Tazolka", "Thosz"]),

        // Legacy day-1 (Monday) group, Tier 2.
        (620533187, "Hoobishan", 2, DayOfWeek.Saturday, new TimeOnly(9, 0), ["Ezla", "Mak'ala", "Perim", "Vemira"]),
        (1556418684, "Zhian", 2, DayOfWeek.Tuesday, new TimeOnly(12, 0), ["Helvi", "Mak'ala", "Tefkari", "Triss"]),
        (906166537, "Crios", 2, DayOfWeek.Tuesday, new TimeOnly(13, 0), ["Adia", "Corva", "Qeyma", "Stilhe"]),
        (2062242845, "Bolari", 2, DayOfWeek.Tuesday, new TimeOnly(14, 0), ["Barasa", "Brijac", "Kolava", "Parturi"]),
        (558058285, "Roshar", 2, DayOfWeek.Tuesday, new TimeOnly(15, 0), ["Avansa", "Corva", "Stilhe", "Thaylen"]),
        (100583287, "Bimasa", 2, DayOfWeek.Tuesday, new TimeOnly(16, 0), ["Beku", "Brellan", "Gelida", "Zamaro"]),
        (1809221684, "Duportas", 2, DayOfWeek.Saturday, new TimeOnly(13, 0), ["Aylus", "Ber'Tho", "Brellan", "Nujord"]),
        (1960606625, "Eldur", 2, DayOfWeek.Tuesday, new TimeOnly(18, 0), ["Hrojost", "Klefaski", "Nyrheimur", "Thosz"]),
        (450781274, "Abilakk", 2, DayOfWeek.Tuesday, new TimeOnly(19, 0), ["Anzat", "Lenara", "Tholus", "Thosz"]),

        // Legacy day-3 (Wednesday) group, Tier 1.
        (75447996, "Adia", 1, DayOfWeek.Wednesday, new TimeOnly(9, 0), ["Crios", "Temeri", "Tezera"]),
        (1414400237, "Otima", 1, DayOfWeek.Wednesday, new TimeOnly(10, 0), ["Aonad", "Avansa", "Ruhe"]),
        (2106408117, "Perim", 1, DayOfWeek.Wednesday, new TimeOnly(11, 0), ["Ezla", "Hoobishan", "Temeri"]),
        (233224244, "Thaylen", 1, DayOfWeek.Wednesday, new TimeOnly(12, 0), ["Burran", "Comst", "Roshar"]),
        (1976653582, "Asiti", 1, DayOfWeek.Wednesday, new TimeOnly(13, 0), ["Kolava", "Parturi", "Qeyma"]),
        (1646136635, "Tefkari", 1, DayOfWeek.Wednesday, new TimeOnly(14, 0), ["Innlasn", "Vantar", "Zhian"]),
        (1170669710, "Nujord", 1, DayOfWeek.Friday, new TimeOnly(14, 0), ["Duportas", "Framtid", "Hrojost"]),
        (1645876992, "Zamaro", 1, DayOfWeek.Wednesday, new TimeOnly(16, 0), ["Ber'Tho", "Bimasa", "Saldeti"]),
        (462342571, "Thosz", 1, DayOfWeek.Wednesday, new TimeOnly(17, 0), ["Abilakk", "Aylus", "Eldur"]),
        (1383107774, "Lenara", 1, DayOfWeek.Monday, new TimeOnly(18, 0), ["Abilakk", "Helvi", "Triss"]),

        // Legacy day-4 (Thursday) group, Tier 2.
        (727367149, "Vantar", 2, DayOfWeek.Saturday, new TimeOnly(10, 0), ["Innlasn", "Klefaski", "Nyrheimur", "Tefkari"]),
        (225845685, "Avansa", 2, DayOfWeek.Saturday, new TimeOnly(11, 0), ["Corva", "Otima", "Roshar", "Ruhe"]),
        (372050647, "Burran", 2, DayOfWeek.Saturday, new TimeOnly(12, 0), ["Barasa", "Comst", "Stilhe", "Thaylen"]),
        (1133579970, "Triss", 2, DayOfWeek.Tuesday, new TimeOnly(17, 0), ["Lenara", "Mak'ala", "Tigan", "Zhian"]),
        (2144111369, "Brijac", 2, DayOfWeek.Saturday, new TimeOnly(14, 0), ["Barasa", "Beku", "Bolari", "Saldeti"]),
        (1457297806, "Hrojost", 2, DayOfWeek.Saturday, new TimeOnly(15, 0), ["Eldur", "Framtid", "Nujord", "Nyrheimur"]),
        (1205790513, "Tezera", 2, DayOfWeek.Saturday, new TimeOnly(16, 0), ["Adia", "Tholus", "Tigan", "Vemira"]),
        (939860675, "Anzat", 2, DayOfWeek.Saturday, new TimeOnly(17, 0), ["Abilakk", "Parturi", "Tazolka", "Tholus"]),
        (2044888998, "Ber'Tho", 2, DayOfWeek.Saturday, new TimeOnly(18, 0), ["Brellan", "Duportas", "Tazolka", "Zamaro"]),

        // Legacy day-5 (Friday) group, Tier 1.
        (647935234, "Comst", 1, DayOfWeek.Friday, new TimeOnly(9, 0), ["Aonad", "Burran", "Thaylen"]),
        (716012578, "Framtid", 1, DayOfWeek.Friday, new TimeOnly(10, 0), ["Hrojost", "Innlasn", "Nujord"]),
        (610044534, "Ruhe", 1, DayOfWeek.Friday, new TimeOnly(11, 0), ["Aonad", "Avansa", "Otima"]),
        (1020076107, "Saldeti", 1, DayOfWeek.Friday, new TimeOnly(12, 0), ["Brijac", "Gelida", "Zamaro"]),
        (1695443788, "Ezla", 1, DayOfWeek.Friday, new TimeOnly(13, 0), ["Hoobishan", "Perim", "Temeri"]),
        (1518413947, "Kolava", 1, DayOfWeek.Wednesday, new TimeOnly(15, 0), ["Asiti", "Bolari", "Qeyma"]),
        (1964344020, "Beku", 1, DayOfWeek.Friday, new TimeOnly(15, 0), ["Bimasa", "Brijac", "Gelida"]),
        (1576679126, "Klefaski", 1, DayOfWeek.Friday, new TimeOnly(16, 0), ["Eldur", "Helvi", "Vantar"]),
        (507519541, "Tazolka", 1, DayOfWeek.Friday, new TimeOnly(17, 0), ["Anzat", "Aylus", "Ber'Tho"]),
        (91087479, "Tigan", 1, DayOfWeek.Friday, new TimeOnly(18, 0), ["Tezera", "Triss", "Vemira"]),

        // Legacy day-6 (Saturday) group, Tier 3.
        (190693683, "Mak'ala", 3, DayOfWeek.Sunday, new TimeOnly(13, 0), ["Hoobishan", "Triss", "Zhian"]),
        (1766074547, "Corva", 3, DayOfWeek.Sunday, new TimeOnly(14, 0), ["Avansa", "Crios", "Roshar"]),
        (851187192, "Nyrheimur", 3, DayOfWeek.Sunday, new TimeOnly(15, 0), ["Eldur", "Hrojost", "Vantar"]),
        (1603382959, "Barasa", 3, DayOfWeek.Sunday, new TimeOnly(16, 0), ["Bolari", "Brijac", "Burran"]),
        (47245576, "Tholus", 3, DayOfWeek.Sunday, new TimeOnly(17, 0), ["Abilakk", "Anzat", "Tezera"]),
        (404484182, "Brellan", 3, DayOfWeek.Sunday, new TimeOnly(18, 0), ["Ber'Tho", "Bimasa", "Duportas"]),

        // New zone (2026-07-04), not in the legacy 55.
        (2088113723, "Qoda", 4, DayOfWeek.Monday, new TimeOnly(19, 0), ["Framtid"]),
    ];

    public static readonly (string ZoneName, string AllianceTag)[] Ownership =
    [
        ("Qeyma", "RETR"),
        ("Gelida", "BCTK"),
        ("Vemira", "LF"),
        ("Helvi", "DSX"),
        ("Temeri", "S3I"),
        ("Stilhe", "DÀRK"),
        ("Parturi", "THOR"),
        ("Aylus", "BONK"),
        ("Hoobishan", "S3I"),
        ("Crios", "DÀRK"),
        ("Roshar", "DÀRK"),
        ("Abilakk", "DSX"),
        ("Adia", "TLE"),
        ("Otima", "GER"),
        ("Perim", "S3I"),
        ("Thaylen", "AEON"),
        ("Asiti", "THOR"),
        ("Tefkari", "GOT"),
        ("Nujord", "KOBY"),
        ("Thosz", "BONK"),
        ("Lenara", "DSX"),
        ("Vantar", "DFÖ"),
        ("Avansa", "DÀRK"),
        ("Burran", "AEON"),
        ("Triss", "DSX"),
        ("Brijac", "KÅØŞ"),
        ("Hrojost", "KOBY"),
        ("Tezera", "LF"),
        ("Ber'Tho", "SHQL"),
        ("Comst", "AEON"),
        ("Framtid", "KOBY"),
        ("Ruhe", "GER"),
        ("Saldeti", "BCTK"),
        ("Ezla", "S3I"),
        ("Kolava", "THOR"),
        ("Beku", "KÅØŞ"),
        ("Klefaski", "DFÖ"),
        ("Tazolka", "SHQL"),
        ("Tigan", "LF"),
        ("Mak'ala", "S3I"),
        ("Nyrheimur", "KOBY"),
        ("Barasa", "KÅØŞ"),
        ("Tholus", "LF"),
        ("Brellan", "SHQL"),
        ("Qoda", "KOBY"),
    ];
}
