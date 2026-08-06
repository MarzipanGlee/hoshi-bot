using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Discord.AiChat;

// Everything that goes INTO the model: the system instruction and each of its grounding blocks —
// retrieved knowledge, latest announcements, member lore, the three memory tiers and the
// Territory Capture facts. Pure prompt-context assembly; the decision/answer pipeline lives in
// AiChatService.cs.
public partial class AiChatService
{
    private async Task<string> BuildSystemInstructionAsync(ulong guildId, ulong channelId, string botName, string? systemExtra, bool addressed, string questionText, IReadOnlyDictionary<ulong, string> mentionable, int knowledgeSnippetLimit, string model, AiProvider providerKind, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine(HoshiPersona.Describe(botName));
        // The answer-language instruction is the one dynamic part of the prompt: the reply speaks
        // the channel scope's resolved language (_replyLanguage). The instruction itself is English
        // for every language — model-facing text is not catalog material and stays in code.
        sb.AppendLine($"Answer in {Languages.EnglishName(_replyLanguage)}. Antworte freundlich und knapp. Nutze zum Beantworten in erster Linie die unten angegebenen verlässlichen Fakten, Wissensquellen und den bisherigen Chatverlauf.");
        sb.AppendLine("Bei Sachfragen (Spielmechaniken, Crews, Aufstellungen, Zahlen, Daten, Ereignisse) sind allein die Wissensquellen, die verlässlichen Fakten und die offiziellen Ankündigungen maßgeblich — sie haben Vorrang vor deinen Erinnerungen und vor allgemeinem Wissen. Wenn diese Quellen eine Sachfrage nicht abdecken, rate nicht und stütze dich nicht auf Erinnerungen, sondern sag ehrlich, dass du es nicht sicher weißt.");
        // The rule has to sit in the base rules, not in the knowledge block: the context blocks below
        // (Ankündigungen, Wissensquellen) all prefix their lines with "[<#ID>]", and a bracketed token
        // looks exactly like markdown link text — a model that never got this rule "completes" it into
        // a [Text](URL) link, which Discord shows raw in a plain message.
        sb.AppendLine("Formatierung von Kanal-Verweisen: Wenn du auf einen Kanal verweist, verwende exakt die Discord-Syntax <#ID> mit einer ID aus den Kontextblöcken unten (dort steht sie in eckigen Klammern vor jeder Zeile); Discord macht daraus einen klickbaren Kanal. Schreibe niemals eine discord.com/channels-URL, niemals [#Name] oder #Name als reinen Text und erfinde keine IDs. Verwende außerdem nie die Markdown-Linksyntax [Text](URL) — deine Nachricht ist eine normale Chat-Nachricht, in der Discord das unverändert als Text anzeigt; nenne eine URL einfach direkt.");

        // Resolve the guild's timezone + culture once here — the environment block, the <t:unix>
        // resolver (Part A) and the knowledge-snippet date prefixes (Part C) all share them. Timezone
        // is an alliance-only setting, so use the message's resolved alliance, falling back to the
        // guild's primary; culture drives the German/English date rendering.
        var alliance = _settingsScope.AllianceId is { } allianceId
            ? await allianceService.FindByIdAsync(guildId, allianceId)
            : await allianceService.GetPrimaryAsync(guildId);
        var tz = GuildAlliance.ResolveTimeZone(alliance?.TimeZoneId);
        var culture = Languages.ToCulture(_replyLanguage);

        sb.AppendLine();
        sb.Append(BuildEnvironmentContext(guildId, model, providerKind, tz, _replyLanguage));

        if (!string.IsNullOrWhiteSpace(systemExtra))
        {
            sb.AppendLine();
            sb.AppendLine(systemExtra.Trim());
        }

        if (mentionable.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Bekannte Nutzer. Um jemanden zu erwähnen oder anzupingen, verwende exakt die Syntax <@ID> mit einer ID aus dieser Liste (niemals eine ID erfinden, niemals @Name als reinen Text schreiben):");
            foreach (var (id, name) in mentionable)
                sb.AppendLine($"- {name}: <@{id}>");
        }

        var conversationMemory = await BuildConversationMemoryBlockAsync(guildId, channelId, cancellationToken);
        if (conversationMemory.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Bisheriger Gesprächsverlauf in diesem Kanal (Zusammenfassungen älterer Nachrichten; die neuesten Nachrichten stehen bereits oben im Verlauf):");
            sb.Append(conversationMemory);
        }

        var memberNotes = await BuildMemberNotesAsync(guildId, cancellationToken);
        if (memberNotes.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Was du über die Mitglieder weißt (nutze das für warme, insider-artige Anspielungen und um dich wie ein echtes Community-Mitglied zu verhalten; du darfst auch über gerade abwesende Mitglieder reden; wenn etwas unpassend wäre, lass es einfach weg):");
            sb.Append(memberNotes);
        }

        var memberMemory = await BuildMemberMemoryBlockAsync(guildId, mentionable, cancellationToken);
        if (memberMemory.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Was du zuletzt mit einzelnen Mitgliedern besprochen hast (nutze es, um persönlich am letzten Gespräch anzuknüpfen, z. B. \"letztes Mal hast du erzählt, dass...\"; nur relevant für die Person, an die es sich richtet):");
            sb.Append(memberMemory);
        }

        var memories = await BuildMemoryBlockAsync(guildId, questionText, cancellationToken);
        if (memories.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Woran du dich aus dem Leben der Community erinnerst (weiche, evtl. ungenaue Erinnerungen aus früheren Gesprächen — nutze sie NUR für lockere, soziale Anspielungen auf frühere Ereignisse, NIEMALS als Quelle für Spielfakten, Mechaniken, Crews, Zahlen oder Daten. Wenn eine Erinnerung den Wissensquellen oder verlässlichen Fakten weiter unten widerspricht, gelten die Quellen — lass die Erinnerung dann weg):");
            sb.Append(memories);
        }

        var facts = await BuildTerritoryCaptureFactsAsync(guildId, cancellationToken);
        if (facts.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Verlässliche Fakten aus der Datenbank (bevorzuge diese vor allgemeinem Wissen und vor unsicheren Chat-Auszügen). Gib Zeitangaben in der Form <t:UNIX:t> unverändert weiter — Discord zeigt sie dann automatisch in der Lokalzeit jedes Nutzers:");
            sb.Append(facts);
        }

        var announcements = await BuildLatestAnnouncementsBlockAsync(guildId, tz, culture, cancellationToken);
        if (announcements.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Neueste offizielle Ankündigungen (die aktuellsten Nachrichten aus den wichtigsten Kanälen — nutze sie für Fragen zu Neuigkeiten, Wartungen, Updates oder Events; sie sind aktueller und verlässlicher als die Wissensquellen unten):");
            sb.Append(announcements);
        }

        var knowledge = await BuildKnowledgeBlockAsync(guildId, questionText, knowledgeSnippetLimit, tz, culture, cancellationToken);
        if (knowledge.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Wissensquellen (relevante Auszüge; in eckigen Klammern stehen der Herkunftskanal als Link <#ID> und das Erstellungsdatum der Nachricht):");
            sb.Append(knowledge);
            sb.AppendLine();
            sb.AppendLine("Jede Zeile in den Wissensquellen ist eine eigenständige Information aus einer einzelnen Nachricht. Verknüpfe keine getrennten Zeilen oder Aufzählungspunkte zu einer neuen Behauptung, die so nirgends steht, auch wenn sie plausibel klingt. Wenn die Wissensquellen eine Frage nicht eindeutig und direkt beantworten, sag ehrlich, dass du es nicht sicher weißt, statt Fakten zu kombinieren oder zu raten. Jede Zeile trägt ihr Erstellungsdatum voran: Bei zeitkritischen oder einander widersprechenden Aussagen (z. B. Event verschoben vs. Event läuft) gilt die neuere; vergleiche das Datum mit dem heutigen Datum aus deiner Umgebung und behandle eine alte Nachricht nicht als aktuell.");
        }

        sb.AppendLine();
        sb.AppendLine(addressed
            ? "Du wirst in dieser Nachricht direkt angesprochen. Antworte immer. Wenn du etwas nicht sicher weißt, sage das ehrlich."
            : $"Du liest in diesem Kanal nur mit und mischst dich NICHT ins Gespräch ein. Antworte ausschließlich dann, wenn die Nachricht eine klare Sachfrage ist, die dir (oder allgemein) gestellt wird und die du mit den Wissensquellen fundiert beantworten kannst. Bei Aussagen, Meinungen, Aufrufen an die Allianz oder an andere Mitglieder, Begrüßungen, Smalltalk, Reaktionen oder allem, was keine an dich gerichtete, beantwortbare Sachfrage ist, antworte ausschließlich mit exakt {NoAnswerSentinel} und sonst nichts. Im Zweifel immer {NoAnswerSentinel}.");

        return sb.ToString();
    }

    // A compact, authoritative "current environment" block: today's date/time in the guild's
    // configured timezone (so Hoshi can reason about relative dates instead of guessing — she was
    // seen agreeing "in 3 Tagen" without knowing today's date), the community name, and the AI model
    // she runs on (so she can answer meta-questions about herself). These are reliable computed
    // facts, so they sit with the trusted top-of-prompt instructions, not the soft memory blocks.
    // tz is the guild's resolved timezone (an alliance-only setting): the caller resolves it once —
    // from the message's resolved alliance on the chat path, or the guild's primary alliance on the
    // admin-compose path — and shares it with the <t:unix> resolver and the knowledge date prefixes.
    // lang is the reply's resolved language and drives the date/weekday rendering (same per-language
    // pattern split as the TC digest's date).
    private string BuildEnvironmentContext(ulong guildId, string model, AiProvider providerKind, TimeZoneInfo tz, Language lang)
    {
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var culture = Languages.ToCulture(lang);
        var nowText = localNow.ToString(lang == Language.En ? "dddd, MMMM d, yyyy, HH:mm" : "dddd, d. MMMM yyyy, HH:mm 'Uhr'", culture);
        var providerName = providerKind == AiProvider.Ollama ? "Ollama, lokal" : "Google Gemini";

        var sb = new StringBuilder();
        sb.AppendLine("Verlässliche Angaben über deine aktuelle Umgebung (du darfst sie bei Bedarf nennen):");
        sb.AppendLine($"- Aktuelles Datum und Uhrzeit: {nowText} (Zeitzone {tz.Id}). Nutze dieses Datum, um relative Zeitangaben („in X Tagen“, „nächstes Turnier“, „übermorgen“) korrekt zu berechnen, statt zu raten.");
        if (gatewayClient.Cache.Guilds.GetValueOrDefault(guildId)?.Name is { Length: > 0 } guildName)
            sb.AppendLine($"- Du bist gerade in der Community „{guildName}“.");
        sb.AppendLine($"- Du läufst aktuell auf dem KI-Modell {model} ({providerName}).");
        return sb.ToString();
    }

    // Discord timestamp tokens (<t:UNIX> / <t:UNIX:style>) render as localized dates in a Discord
    // client, but to the model they're opaque integers it can neither read nor reason about. STFC event
    // announcements put all their dates/times in these tokens, so a retrieved announcement arrives
    // date-blind — the model can't tell "when". Rewrite each token to a concrete human-readable
    // date/time in the guild's timezone. Rendered readable-only (the raw token is dropped): the guild is
    // single-timezone, so a concrete local time beats a token the model would mangle on output. Only the
    // TC facts block keeps raw <t:…:t> tokens (it explicitly tells the model to pass them through for
    // per-user localization). An unparseable/out-of-range token is left untouched.
    private static string ResolveDiscordTimestamps(string text, TimeZoneInfo tz, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("<t:", StringComparison.Ordinal))
            return text;

        var german = culture.TwoLetterISOLanguageName != "en";
        return DiscordTimestampRegex().Replace(text, match =>
        {
            if (!long.TryParse(match.Groups[1].Value, out var unix))
                return match.Value;
            DateTimeOffset local;
            try
            {
                local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(unix), tz);
            }
            catch (ArgumentOutOfRangeException)
            {
                return match.Value; // unix outside DateTimeOffset's representable range
            }

            // Discord's styles: t/T time, d/D date, f/F (and the default) date+time, R relative. Render
            // R and F as an absolute long date+time (best for the model's reasoning); f/default as a
            // short date+time. Weekday/month names follow the reply language.
            var style = match.Groups[2].Success ? match.Groups[2].Value[0] : 'f';
            var pattern = style switch
            {
                't' => german ? "HH:mm 'Uhr'" : "h:mm tt",
                'T' => german ? "HH:mm:ss 'Uhr'" : "h:mm:ss tt",
                'd' => german ? "dd.MM.yyyy" : "MM/dd/yyyy",
                'D' => german ? "d. MMMM yyyy" : "MMMM d, yyyy",
                'F' or 'R' => german ? "dddd, d. MMMM yyyy, HH:mm 'Uhr'" : "dddd, MMMM d, yyyy, h:mm tt",
                _ => german ? "d. MMMM yyyy, HH:mm 'Uhr'" : "MMMM d, yyyy, h:mm tt", // 'f' and the default
            };
            return local.ToString(pattern, culture);
        });
    }

    // <t:1785571200> or <t:1785571200:F> — the (possibly negative) unix seconds plus an optional
    // one-letter style, matching Discord's timestamp markup exactly.
    [GeneratedRegex(@"<t:(-?\d+)(?::([tTdDfFR]))?>")]
    private static partial Regex DiscordTimestampRegex();

    // The grounding block: the messages from the guild's knowledge index most relevant to the
    // question (full-text search). Falls back to a live gather only while the index has no content
    // for the guild yet (before the first backfill), so early questions still work.
    private async Task<string> BuildKnowledgeBlockAsync(ulong guildId, string questionText, int knowledgeSnippetLimit, TimeZoneInfo tz, CultureInfo culture, CancellationToken cancellationToken)
    {
        if (!await indexService.HasIndexedContentAsync(guildId, cancellationToken))
            return await indexService.GetRecentKnowledgeFallbackAsync(guildId, cancellationToken);

        var language = await ResolveSearchLanguageAsync(guildId);
        var hits = await indexService.SearchAsync(guildId, language, questionText, knowledgeSnippetLimit, cancellationToken);

        // Trace what retrieval actually surfaced, so "why didn't she know X?" is answerable from logs
        // (the top hit channels + snippet heads for this question).
        logger.LogInformation("AiChat knowledge hits guild {Guild} q=\"{Question}\": [{Hits}]",
            guildId, questionText,
            string.Join(" | ", hits.Select(h => $"{h.ChannelName}: {h.Content[..Math.Min(50, h.Content.Length)].Replace('\n', ' ')}")));

        // Each snippet is prefixed with its post's creation date/time in the guild timezone (Part C) so
        // Hoshi can weigh recency and not narrate a week-old post as current, and its inline <t:unix>
        // event timestamps are resolved to readable dates (Part A) so she can read the actual event time.
        var sb = new StringBuilder();
        foreach (var hit in hits)
        {
            var date = TimeZoneInfo.ConvertTime(hit.CreatedAt, tz).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var content = ResolveDiscordTimestamps(hit.Content, tz, culture);
            sb.AppendLine(hit.ChannelId != 0 ? $"- [<#{hit.ChannelId}>] ({date}) {content}" : $"- ({date}) {content}");
        }

        return sb.ToString();
    }

    // Always-current "latest announcements": the most recent messages from the guild's Preferred
    // knowledge channels (e.g. official-announcements), live-fetched so a just-posted notice is in
    // context immediately. This sidesteps both the index/embedding lag and the semantic-ranking miss
    // that buries a time-sensitive fact (like a maintenance date) inside a long announcement — those
    // never rank well, but they're always here regardless. Skips the bot's own messages.
    // Fetch depth PER Preferred channel (a busy general-announcements channel can push a still-relevant
    // notice several posts down, and the bot's-own-message skip eats into the fetch), the global
    // newest-first cap across all Preferred channels, the per-snippet char cap and the total budget.
    // Sized so a time-sensitive announcement that sits ~a dozen posts deep across the Preferred channels
    // (e.g. an event announced days before it runs) still lands in the block.
    private const int LatestAnnouncementsFetchPerChannel = 12;
    private const int LatestAnnouncementsMaxShown = 15;
    private const int LatestAnnouncementCharCap = 500;
    private const int LatestAnnouncementsCharBudget = 7500;

    private async Task<string> BuildLatestAnnouncementsBlockAsync(ulong guildId, TimeZoneInfo tz, CultureInfo culture, CancellationToken cancellationToken)
    {
        // Preferred knowledge channels across every enabled audience (same source SearchAsync tiers on).
        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, GuildFeature.AiChat);
        var preferredChannels = new HashSet<ulong>();
        foreach (var audience in enabledAudiences)
            preferredChannels.UnionWith(await channelService.GetChannelsAsync(guildId, GuildFeature.AiChatKnowledgePreferred, audience));
        if (preferredChannels.Count == 0)
            return "";

        // A Preferred entry may be a whole category or a forum; neither holds messages of its own,
        // so drop those before fetching rather than firing a doomed REST call per answer. Their
        // content still reaches the answer through the index (the indexer expands both).
        var fetchable = await indexService.FilterDirectMessageSourcesAsync(guildId, preferredChannels, cancellationToken);

        var messages = new List<(DateTimeOffset When, ulong ChannelId, string Text)>();
        foreach (var channelId in fetchable)
        {
            try
            {
                foreach (var message in await indexService.FetchRecentAsync(channelId, LatestAnnouncementsFetchPerChannel, cancellationToken) ?? [])
                {
                    if (message.Author.Id == gatewayClient.Id)
                        continue;
                    var text = AiChatIndexService.RenderMessageText(message);
                    if (!string.IsNullOrWhiteSpace(text))
                        messages.Add((message.CreatedAt, channelId, text));
                }
            }
            catch (Exception ex)
            {
                // An inaccessible channel just contributes nothing.
                logger.LogWarning(ex, "Latest-announcements fetch failed for channel {ChannelId}", channelId);
            }
        }

        var sb = new StringBuilder();
        foreach (var (when, channelId, text) in messages.OrderByDescending(m => m.When).Take(LatestAnnouncementsMaxShown))
        {
            // Resolve inline <t:unix> event timestamps to readable dates (Part A) before trimming, so the
            // char cap counts the readable text the model actually sees.
            var resolved = ResolveDiscordTimestamps(text, tz, culture);
            var trimmed = resolved.Length > LatestAnnouncementCharCap ? resolved[..LatestAnnouncementCharCap] + "…" : resolved;
            var date = TimeZoneInfo.ConvertTime(when, tz).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var line = $"- [<#{channelId}>] ({date}) {trimmed.Replace('\n', ' ')}";
            if (sb.Length + line.Length > LatestAnnouncementsCharBudget)
                break;
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    // The community-lore block: one compact line per member with a GuildMemberNote, so Hoshi behaves
    // like a real member who knows the whole cast — including people not currently in the conversation
    // (the "ensemble" effect). Only the live, approved fields inject; suggestions never do, and a
    // member's peer-lore veto (PeerLoreHidden) drops the peer fields. Gated on the MemberLore feature
    // and capped by a character budget so a big roster can't blow up the prompt. See docs/ai-chat-member-lore.md.
    private const int MemberNotesCharBudget = 4000;

    private async Task<string> BuildMemberNotesAsync(ulong guildId, CancellationToken cancellationToken)
    {
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.MemberLore))
            return "";

        var notes = await memberNoteService.GetForGuildAsync(guildId, cancellationToken);
        if (notes.Count == 0)
            return "";

        // Consolidate a person's accounts (player-linked alts share a person key) into one entry so
        // Hoshi treats them as the same commander and uses lore stored on either account.
        var personKeys = await memberNoteService.GetPersonKeysAsync(notes.Select(n => n.DiscordUserId), cancellationToken);

        var sb = new StringBuilder();
        foreach (var group in notes.GroupBy(n => personKeys.GetValueOrDefault(n.DiscordUserId, $"user:{n.DiscordUserId}")))
        {
            var ids = group
                .OrderByDescending(n => string.IsNullOrWhiteSpace(n.PreferredName) ? 0 : 1)
                .ThenBy(n => n.DiscordUserId)
                .Select(n => n.DiscordUserId)
                .ToList();
            var line = RenderPersonNote(ids, MergePersonNotes(group));
            if (line is null)
                continue;
            if (sb.Length + line.Length > MemberNotesCharBudget)
                break;
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    // Combine a person's per-account notes into one view: first non-empty preferred name, distinct
    // values per field, and peer fields dropped if *any* of the person's accounts vetoed them.
    private static GuildMemberNote MergePersonNotes(IEnumerable<GuildMemberNote> group)
    {
        var list = group.ToList();
        var peerHidden = list.Any(n => n.PeerLoreHidden);
        return new GuildMemberNote
        {
            PreferredName = list.Select(n => n.PreferredName).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
            Nicknames = MergeField(list.Select(n => n.Nicknames)),
            Interests = MergeField(list.Select(n => n.Interests)),
            Background = MergeField(list.Select(n => n.Background)),
            Languages = MergeField(list.Select(n => n.Languages)),
            RunningJokes = peerHidden ? null : MergeField(list.Select(n => n.RunningJokes)),
            TeaseAbout = peerHidden ? null : MergeField(list.Select(n => n.TeaseAbout)),
        };
    }

    private static string? MergeField(IEnumerable<string?> values)
    {
        var parts = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Inline(v!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    // One compact line for a person, listing all their account mentions so the bot can ping either.
    // Fields are already merged + peer-veto-applied by MergePersonNotes.
    private static string? RenderPersonNote(IReadOnlyList<ulong> ids, GuildMemberNote note)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(note.Nicknames)) parts.Add($"auch {note.Nicknames}");
        if (!string.IsNullOrWhiteSpace(note.Interests)) parts.Add($"mag {note.Interests}");
        if (!string.IsNullOrWhiteSpace(note.Background)) parts.Add(note.Background);
        if (!string.IsNullOrWhiteSpace(note.Languages)) parts.Add($"spricht {note.Languages}");
        if (!string.IsNullOrWhiteSpace(note.RunningJokes)) parts.Add($"Running Gags: {note.RunningJokes}");
        if (!string.IsNullOrWhiteSpace(note.TeaseAbout)) parts.Add($"necken ok: {note.TeaseAbout}");

        if (parts.Count == 0)
            return null;

        var name = string.IsNullOrWhiteSpace(note.PreferredName) ? "" : $"{note.PreferredName.Trim()} ";
        var mentions = string.Join(", ", ids.Select(id => $"<@{id}>"));
        return $"- {name}({mentions}): {string.Join("; ", parts)}";
    }

    // Flatten a multi-line note field (fields accumulate appended lines) into one line for the compact block.
    private static string Inline(string value) => value.Replace("\r", "").Replace("\n", "; ").Trim();

    // Hoshi's episodic memory (Phase 1): the community events she's formed and can recall. Retrieves
    // the ones most relevant to the current question (semantic search) plus a few recent + salient
    // ones, so she can reference past happenings like a real member. Reinforces what it surfaces (so
    // used memories resist decay). Gated on AiChatSettingKeys.MemoryEnabled. See the memory plan.
    private const int MemoryCharBudget = 3000;
    private const int MemoryRelevantLimit = 6;
    private const int MemoryRecentLimit = 3;

    private async Task<string> BuildMemoryBlockAsync(ulong guildId, string questionText, CancellationToken cancellationToken)
    {
        var enabled = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, _settingsScope.Audience, _settingsScope.AllianceId, AiChatSettingKeys.MemoryEnabled);
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            return "";

        var memories = new List<GuildMemory>();
        if (!string.IsNullOrWhiteSpace(questionText) && await embeddingService.IsEnabledAsync(guildId)
            && await embeddingService.EmbedAsync(guildId, questionText, cancellationToken) is { } queryVec)
        {
            var model = await embeddingService.GetModelAsync(guildId);
            memories.AddRange(await memoryService.SearchEpisodicAsync(guildId, queryVec, model, MemoryRelevantLimit, cancellationToken));
        }

        // Always fold in a few recent + salient ones so standout events surface even when the current
        // question doesn't match them semantically.
        foreach (var recent in await memoryService.GetRecentSalientAsync(guildId, MemoryScope.Episodic, MemoryRecentLimit, cancellationToken))
            if (memories.All(m => m.Id != recent.Id))
                memories.Add(recent);

        if (memories.Count == 0)
            return "";

        var sb = new StringBuilder();
        foreach (var memory in memories)
        {
            var line = $"- ({memory.CreatedAt:yyyy-MM-dd}) {Inline(memory.Content)}";
            if (sb.Length + line.Length > MemoryCharBudget)
                break;
            sb.AppendLine(line);
        }

        // Reinforce what we actually surfaced so genuinely useful memories resist decay.
        await memoryService.ReinforceAsync(memories.Select(m => m.Id), cancellationToken);

        return sb.ToString();
    }

    // Hoshi's longer conversation memory (Phase 2): the current channel's recent conversation snapshots
    // — summaries of what was discussed here before the messages that are still in the live window — so
    // a thread survives past the 15-message window. Recency-scoped to this channel; gated on MemoryEnabled.
    private const int ConversationSnapshotLimit = 4;

    private async Task<string> BuildConversationMemoryBlockAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken)
    {
        var enabled = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, _settingsScope.Audience, _settingsScope.AllianceId, AiChatSettingKeys.MemoryEnabled);
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            return "";

        var snapshots = await memoryService.GetRecentConversationAsync(guildId, channelId, ConversationSnapshotLimit, cancellationToken);
        if (snapshots.Count == 0)
            return "";

        var sb = new StringBuilder();
        foreach (var snapshot in snapshots)
            sb.AppendLine($"- ({snapshot.CreatedAt:yyyy-MM-dd HH:mm}) {Inline(snapshot.Content)}");

        await memoryService.ReinforceAsync(snapshots.Select(m => m.Id), cancellationToken);
        return sb.ToString();
    }

    // Hoshi's per-member interaction memory (Phase 3): a conversational recap for whoever is actually
    // taking part in this exchange right now — "letztes Mal hast du mir erzählt...". Deliberately
    // scoped to conversation PARTICIPANTS only, unlike the always-on-for-everyone member-lore notes
    // block above: a personal recollection only makes sense addressed to that person directly, not as
    // a third-person aside about someone who isn't here. Person-key resolution consolidates a
    // participant's linked alt accounts onto the same memories.
    private const int MemberMemoryLimit = 4;

    private async Task<string> BuildMemberMemoryBlockAsync(ulong guildId, IReadOnlyDictionary<ulong, string> mentionable, CancellationToken cancellationToken)
    {
        var enabled = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, _settingsScope.Audience, _settingsScope.AllianceId, AiChatSettingKeys.MemoryEnabled);
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase) || mentionable.Count == 0)
            return "";

        var personKeyByUser = await memberNoteService.GetPersonKeysAsync(mentionable.Keys, cancellationToken);
        var sb = new StringBuilder();
        var recalledIds = new List<int>();

        foreach (var personKey in personKeyByUser.Values.Distinct())
        {
            var memories = await memoryService.GetRecentMemberAsync(guildId, personKey, MemberMemoryLimit, cancellationToken);
            if (memories.Count == 0)
                continue;

            // Label the block with every mentionable account sharing this person key (an alt-account pair).
            var ids = personKeyByUser.Where(kv => kv.Value == personKey).Select(kv => kv.Key);
            var name = mentionable.Where(kv => personKeyByUser.GetValueOrDefault(kv.Key) == personKey).Select(kv => kv.Value).FirstOrDefault() ?? "";
            sb.AppendLine($"{name} ({string.Join(", ", ids.Select(id => $"<@{id}>"))}):");
            foreach (var memory in memories)
                sb.AppendLine($"- ({memory.CreatedAt:yyyy-MM-dd}) {Inline(memory.Content)}");
            recalledIds.AddRange(memories.Select(m => m.Id));
        }

        if (recalledIds.Count > 0)
            await memoryService.ReinforceAsync(recalledIds, cancellationToken);
        return sb.ToString();
    }

    // Structured, authoritative grounding straight from the DB (not fuzzy chat snippets): this week's
    // Territory Capture zones for the guild's TC-enabled alliances — owned zone, tier and capture
    // window — so the bot can answer "which zones do we hold / when is the next capture?" directly
    // instead of deferring to the digest channel. Times are pre-rendered as Discord timestamps so the
    // model can relay them verbatim and Discord localizes them for each reader. Empty when the guild
    // runs no Territory Capture alliances (then this block is simply omitted).
    private async Task<string> BuildTerritoryCaptureFactsAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var links = await territoryCaptureDigest.GetTcEnabledLinksAsync(guildId);
        if (links.Count == 0)
            return "";

        var weekStart = TerritoryCaptureScheduler.GetWeekStart(DateTimeOffset.UtcNow);
        var culture = Languages.ToCulture(_replyLanguage);
        var sb = new StringBuilder();

        foreach (var link in links)
        {
            var slots = await territoryCaptureDigest.GetWeeklySlotAssignmentsAsync(link.StfcAllianceId, weekStart);
            if (slots.Count == 0)
                continue;

            sb.AppendLine($"Gebietsübernahmen dieser Woche für die Allianz [{link.StfcAlliance.Tag}]:");
            foreach (var (_, territory, start, end) in slots)
            {
                var day = start.ToString("dddd", culture);
                sb.AppendLine($"- {territory.Name} (Tier {territory.Tier}): {day}, <t:{start.ToUnixTimeSeconds()}:t>–<t:{end.ToUnixTimeSeconds()}:t>");
            }
        }

        return sb.ToString();
    }
}
