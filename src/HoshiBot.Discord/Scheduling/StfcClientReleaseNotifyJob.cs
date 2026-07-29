using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Announces a new STFC game client version release per platform. Ported from the legacy
// hoshi-bot-php StfcClientReleaseNotifier (never carried over to this rewrite) — same 3
// implemented sources (Windows/macOS via Xsolla's XML update feed, Android via a Play Store
// page scrape, iOS via Apple's official iTunes Lookup API, which replaces the PHP version's
// fragile App Store page-scrape entirely), same "Linux has no known source" gap. Each
// platform's fetch is isolated in its own try/catch — one platform's failure must not block
// the others, and (unlike the PHP version, which swallows every exception silently) failures
// are actually logged here.
//
// Unlike its sibling notify jobs (which only ever diff pre-seeded rows), this one creates a
// baseline StfcClientRelease row the first time it sees a platform — DisallowConcurrentExecution
// prevents two overlapping runs (e.g. an immediate first fire plus the next scheduled tick
// landing before slow HTTP scrapes finish) from both seeing "no row yet" for the same platform
// and colliding on the unique Platform key.
[DisallowConcurrentExecution]
public class StfcClientReleaseNotifyJob(
    IHttpClientFactory httpClientFactory,
    HoshiBotDbContext db,
    NotificationDispatcher dispatcher,
    GuildFeatureChannelService featureChannelService,
    GuildFeatureSettingsService settingsService,
    EmbedBranding embedBranding,
    LanguageResolver languageResolver,
    ILogger<StfcClientReleaseNotifyJob> logger) : IJob
{
    private const string XsollaUpdatesUrl = "https://gus.xsolla.com/updates?project_id=152033&platform={0}";
    private const string PlayStoreUrl = "https://play.google.com/store/apps/details?id=com.scopely.startrek";
    private const string ITunesLookupUrl = "https://itunes.apple.com/lookup?id=1427744264";

    public async Task Execute(IJobExecutionContext context)
    {
        foreach (var platform in Enum.GetValues<StfcClientPlatform>())
        {
            string? version;
            try
            {
                version = await FetchVersionAsync(platform, context.CancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException
                or System.Text.Json.JsonException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Could not fetch client version for platform {Platform}.", platform);
                continue;
            }

            if (string.IsNullOrEmpty(version))
                continue;

            var row = await db.StfcClientReleases.FindAsync([platform], context.CancellationToken);
            if (row is null)
            {
                // First time ever seeing this platform — establish a baseline, don't announce it.
                db.StfcClientReleases.Add(new StfcClientRelease
                {
                    Platform = platform,
                    Version = version,
                    NotifiedVersion = version,
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
                continue;
            }

            row.Version = version;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            if (row.Version == row.NotifiedVersion)
                continue;

            // Each platform pings only its own guild-wide role (never gets here for Linux — no
            // source produces a version — but RoleKey guards it regardless).
            var roleKey = ClientReleaseSettingKeys.RoleKey(platform);
            if (roleKey is null)
                continue;

            // Every guild that has configured at least one ClientRelease announcement channel.
            var guildIds = await db.GuildFeatureChannels
                .Where(c => c.Feature == GuildFeature.ClientRelease)
                .Select(c => c.GuildId)
                .Distinct()
                .ToListAsync(context.CancellationToken);

            foreach (var guildId in guildIds)
            {
                var channelIds = await featureChannelService.GetEnabledAudienceChannelsAsync(guildId, GuildFeature.ClientRelease);
                if (channelIds.Count == 0)
                    continue;

                var roleId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.ClientRelease, GuildAudience.None, null, roleKey);

                // GuildFeatureChannel rows are audience-scoped (never per-alliance), so each
                // channel renders in its audience's language — grouped so a language's content/
                // embed is built once and sent to all its channels in one dispatcher call.
                var channelsByLanguage = new Dictionary<Language, List<ulong>>();
                foreach (var channelId in channelIds)
                {
                    var lang = await ResolveChannelLanguageAsync(guildId, channelId, context.CancellationToken);
                    (channelsByLanguage.TryGetValue(lang, out var list) ? list : channelsByLanguage[lang] = []).Add(channelId);
                }

                foreach (var (lang, langChannelIds) in channelsByLanguage)
                {
                    var content = BuildContent(platform, version, lang);
                    var embed = await embedBranding.BuildBrandedAsync(guildId, content, EmbedBranding.InformationColor, Msg.Client.NewVersionTitle(lang, DisplayName(platform)));
                    await dispatcher.SendToChannelIdsAsync(guildId, langChannelIds, roleId, content, embed);
                }
            }

            row.NotifiedVersion = row.Version;
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }

    // The language a feature channel's announcement renders in — the same audience rule the
    // dispatcher's alert-channel fan-out applies: Alliance rows carry no alliance id (see
    // GuildFeatureChannel), so Alliance/Guild/None resolve to the guild language, the rest per
    // audience. Falls back to the guild language if the row vanished mid-run.
    private async Task<Language> ResolveChannelLanguageAsync(ulong guildId, ulong channelId, CancellationToken ct)
    {
        var audience = await db.GuildFeatureChannels
            .Where(c => c.GuildId == guildId && c.Feature == GuildFeature.ClientRelease && c.ChannelId == channelId)
            .Select(c => (GuildAudience?)c.Audience)
            .FirstOrDefaultAsync(ct);

        return audience is null or GuildAudience.Alliance or GuildAudience.Guild or GuildAudience.None
            ? await languageResolver.ForGuildAsync(guildId)
            : await languageResolver.ForAudienceAsync(guildId, audience.Value);
    }

    private static string BuildContent(StfcClientPlatform platform, string version, Language lang) => platform switch
    {
        // Store names are proper nouns and stay code-side arguments.
        StfcClientPlatform.Android => Msg.Client.ReleasedOnStore(lang, version, "Google Play Store"),
        StfcClientPlatform.IOS => Msg.Client.ReleasedOnStore(lang, version, "Apple App Store"),
        _ => Msg.Client.Released(lang, version),
    };

    private static string DisplayName(StfcClientPlatform platform) => platform switch
    {
        StfcClientPlatform.IOS => "iOS",
        _ => platform.ToString(),
    };

    private Task<string?> FetchVersionAsync(StfcClientPlatform platform, CancellationToken ct) => platform switch
    {
        StfcClientPlatform.Windows or StfcClientPlatform.MacOS => FetchComputerVersionAsync(platform, ct),
        StfcClientPlatform.Android => FetchAndroidVersionAsync(ct),
        StfcClientPlatform.IOS => FetchIosVersionAsync(ct),
        _ => Task.FromResult<string?>(null), // Linux: no known source, same gap the PHP version had
    };

    // Windows/macOS — Xsolla's XML update-check feed, the pc/mac clients' official distributor.
    private async Task<string?> FetchComputerVersionAsync(StfcClientPlatform platform, CancellationToken ct)
    {
        var xsollaPlatform = platform == StfcClientPlatform.Windows ? "windows" : "mac_os";
        var client = httpClientFactory.CreateClient(nameof(StfcClientReleaseNotifyJob));
        var xml = await client.GetStringAsync(string.Format(XsollaUpdatesUrl, xsollaPlatform), ct);
        var doc = XDocument.Parse(xml);

        foreach (var action in doc.Descendants("action"))
        {
            if ((string?)action.Attribute("type") == "version")
                return (string?)action.Attribute("version");
        }

        return null;
    }

    // Android — no official public version-check API exists (Google's Play Developer Reporting
    // API requires OAuth as the app's OWN developer, unusable for a third-party app like this),
    // so this scrapes the Play Store details page's embedded "ds:5" data blob, same as the
    // legacy PHP version. Uses a true bracket-depth-aware substring extraction instead of the
    // PHP version's naive "first [ to the first ] after it," which can silently truncate if any
    // nested array appears before the real end.
    private async Task<string?> FetchAndroidVersionAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(nameof(StfcClientReleaseNotifyJob));
        var html = await client.GetStringAsync(PlayStoreUrl, ct);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var scriptNode = doc.DocumentNode.SelectSingleNode("//script[@class='ds:5']");
        if (scriptNode is null)
            return null;

        var arrayText = ExtractBalancedBracketSubstring(scriptNode.InnerText, '[', ']');
        if (arrayText is null)
            return null;

        var node = JsonNode.Parse($"{{\"data\":[{arrayText}]}}");
        return node?["data"]?[1]?[2]?[140]?[0]?[0]?[0]?.GetValue<string>();
    }

    // iOS — Apple's official, public, no-auth iTunes Lookup API returns the current version
    // directly; this replaces the legacy PHP version's fragile App Store page-scrape (which
    // dug through a double-JSON-encoded "shoebox" cache blob) entirely.
    private async Task<string?> FetchIosVersionAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(nameof(StfcClientReleaseNotifyJob));
        var json = await client.GetStringAsync(ITunesLookupUrl, ct);
        var node = JsonNode.Parse(json);
        return node?["results"]?[0]?["version"]?.GetValue<string>();
    }

    // Finds the substring between the first occurrence of `open` and its true matching `close`
    // (tracking nesting depth), not just the first `close` encountered afterward.
    private static string? ExtractBalancedBracketSubstring(string text, char open, char close)
    {
        var start = text.IndexOf(open);
        if (start < 0)
            return null;

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == open)
                depth++;
            else if (text[i] == close)
            {
                depth--;
                if (depth == 0)
                    return text[(start + 1)..i];
            }
        }

        return null;
    }
}
