using System.Globalization;
using System.Text;
using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Discord.AiChat;

// Everything that goes INTO the model: the system instruction and each of its grounding blocks —
// retrieved knowledge, latest announcements, member lore, the three memory tiers and the
// Territory Capture facts. Pure prompt-context assembly; the decision/answer pipeline lives in
// AiChatService.cs.
public partial class AiChatService
{
    private async Task<string> BuildSystemInstructionAsync(ulong guildId, ulong channelId, string botName, string? systemExtra, bool addressed, string questionText, IReadOnlyDictionary<ulong, string> mentionable, int knowledgeSnippetLimit, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine(HoshiPersona.Describe(botName));
        sb.AppendLine("Antworte auf Deutsch, freundlich und knapp. Nutze zum Beantworten in erster Linie die unten angegebenen verlässlichen Fakten, Wissensquellen und den bisherigen Chatverlauf.");
        sb.AppendLine("Bei Sachfragen (Spielmechaniken, Crews, Aufstellungen, Zahlen, Daten, Ereignisse) sind allein die Wissensquellen, die verlässlichen Fakten und die offiziellen Ankündigungen maßgeblich — sie haben Vorrang vor deinen Erinnerungen und vor allgemeinem Wissen. Wenn diese Quellen eine Sachfrage nicht abdecken, rate nicht und stütze dich nicht auf Erinnerungen, sondern sag ehrlich, dass du es nicht sicher weißt.");

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

        var announcements = await BuildLatestAnnouncementsBlockAsync(guildId, cancellationToken);
        if (announcements.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Neueste offizielle Ankündigungen (die aktuellsten Nachrichten aus den wichtigsten Kanälen — nutze sie für Fragen zu Neuigkeiten, Wartungen, Updates oder Events; sie sind aktueller und verlässlicher als die Wissensquellen unten):");
            sb.Append(announcements);
        }

        var knowledge = await BuildKnowledgeBlockAsync(guildId, questionText, knowledgeSnippetLimit, cancellationToken);
        if (knowledge.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Wissensquellen (relevante Auszüge; der Herkunftskanal steht als Link <#ID> in eckigen Klammern voran):");
            sb.Append(knowledge);
            sb.AppendLine();
            sb.AppendLine("Wenn du auf einen Kanal verweist, verwende exakt die Discord-Link-Syntax <#ID> mit einer ID aus den Wissensquellen (Discord macht daraus einen klickbaren Link). Schreibe niemals [#Name] oder #Name als reinen Text und erfinde keine IDs.");
            sb.AppendLine("Jede Zeile in den Wissensquellen ist eine eigenständige Information aus einer einzelnen Nachricht. Verknüpfe keine getrennten Zeilen oder Aufzählungspunkte zu einer neuen Behauptung, die so nirgends steht, auch wenn sie plausibel klingt. Wenn die Wissensquellen eine Frage nicht eindeutig und direkt beantworten, sag ehrlich, dass du es nicht sicher weißt, statt Fakten zu kombinieren oder zu raten.");
        }

        sb.AppendLine();
        sb.AppendLine(addressed
            ? "Du wirst in dieser Nachricht direkt angesprochen. Antworte immer. Wenn du etwas nicht sicher weißt, sage das ehrlich."
            : $"Du liest in diesem Kanal nur mit und mischst dich NICHT ins Gespräch ein. Antworte ausschließlich dann, wenn die Nachricht eine klare Sachfrage ist, die dir (oder allgemein) gestellt wird und die du mit den Wissensquellen fundiert beantworten kannst. Bei Aussagen, Meinungen, Aufrufen an die Allianz oder an andere Mitglieder, Begrüßungen, Smalltalk, Reaktionen oder allem, was keine an dich gerichtete, beantwortbare Sachfrage ist, antworte ausschließlich mit exakt {NoAnswerSentinel} und sonst nichts. Im Zweifel immer {NoAnswerSentinel}.");

        return sb.ToString();
    }

    // The grounding block: the messages from the guild's knowledge index most relevant to the
    // question (full-text search). Falls back to a live gather only while the index has no content
    // for the guild yet (before the first backfill), so early questions still work.
    private async Task<string> BuildKnowledgeBlockAsync(ulong guildId, string questionText, int knowledgeSnippetLimit, CancellationToken cancellationToken)
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

        var sb = new StringBuilder();
        foreach (var hit in hits)
            sb.AppendLine(hit.ChannelId != 0 ? $"- [<#{hit.ChannelId}>] {hit.Content}" : $"- {hit.Content}");

        return sb.ToString();
    }

    // Always-current "latest announcements": the most recent messages from the guild's Preferred
    // knowledge channels (e.g. official-announcements), live-fetched so a just-posted notice is in
    // context immediately. This sidesteps both the index/embedding lag and the semantic-ranking miss
    // that buries a time-sensitive fact (like a maintenance date) inside a long announcement — those
    // never rank well, but they're always here regardless. Skips the bot's own messages.
    private const int LatestAnnouncementsCount = 5;
    private const int LatestAnnouncementCharCap = 700;
    private const int LatestAnnouncementsCharBudget = 3500;

    private async Task<string> BuildLatestAnnouncementsBlockAsync(ulong guildId, CancellationToken cancellationToken)
    {
        // Preferred knowledge channels across every enabled audience (same source SearchAsync tiers on).
        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, GuildFeature.AiChat);
        var preferredChannels = new HashSet<ulong>();
        foreach (var audience in enabledAudiences)
            preferredChannels.UnionWith(await channelService.GetChannelsAsync(guildId, GuildFeature.AiChatKnowledgePreferred, audience));
        if (preferredChannels.Count == 0)
            return "";

        var messages = new List<(DateTimeOffset When, ulong ChannelId, string Text)>();
        foreach (var channelId in preferredChannels)
        {
            try
            {
                foreach (var message in await indexService.FetchRecentAsync(channelId, LatestAnnouncementsCount, cancellationToken))
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
                // A category id or an inaccessible channel just contributes nothing.
                logger.LogWarning(ex, "Latest-announcements fetch failed for channel {ChannelId}", channelId);
            }
        }

        var sb = new StringBuilder();
        foreach (var (when, channelId, text) in messages.OrderByDescending(m => m.When).Take(LatestAnnouncementsCount))
        {
            var trimmed = text.Length > LatestAnnouncementCharCap ? text[..LatestAnnouncementCharCap] + "…" : text;
            var line = $"- [<#{channelId}>] ({when:yyyy-MM-dd}) {trimmed.Replace('\n', ' ')}";
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
        var german = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder();

        foreach (var link in links)
        {
            var slots = await territoryCaptureDigest.GetWeeklySlotAssignmentsAsync(link.StfcAllianceId, weekStart);
            if (slots.Count == 0)
                continue;

            sb.AppendLine($"Gebietsübernahmen dieser Woche für die Allianz [{link.StfcAlliance.Tag}]:");
            foreach (var (_, territory, start, end) in slots)
            {
                var day = start.ToString("dddd", german);
                sb.AppendLine($"- {territory.Name} (Tier {territory.Tier}): {day}, <t:{start.ToUnixTimeSeconds()}:t>–<t:{end.ToUnixTimeSeconds()}:t>");
            }
        }

        return sb.ToString();
    }
}
