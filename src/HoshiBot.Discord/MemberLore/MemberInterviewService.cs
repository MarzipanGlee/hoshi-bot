using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.MemberLore;

// Runs the member-lore DM interview: sends the opener (from the invite job), holds the multi-turn DM
// conversation (reusing the guild's AI-chat model), and stores the transcript for later note
// extraction. The bot mirrors the member's language, wraps up when it has enough, and stops on
// opt-out. See docs/ai-chat-member-lore.md.
public class MemberInterviewService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    AiChatModelResolver modelResolver,
    NotificationDispatcher notificationDispatcher,
    GuildFeatureSettingsService settingsService,
    EmbedBranding embedBranding,
    ILogger<MemberInterviewService> logger)
{
    public const string DeclineButtonId = "member-interview-decline";

    // The model appends this on its own line when it has learned enough; stripped before sending.
    private const string DoneSentinel = "[INTERVIEW_DONE]";

    // Safety cap so an interview can't run forever; on the last turn the model is told to wrap up.
    private const int MaxTurns = 14;

    // Sends the opener DM and creates the interview. Returns true if the DM went through (counts
    // against the daily budget); false if the member's DMs are closed (recorded Undeliverable) or an
    // interview already exists.
    public async Task<bool> InviteAsync(ulong guildId, int guildAllianceId, ulong userId, CancellationToken cancellationToken)
    {
        if (await db.MemberInterviews.AnyAsync(i => i.GuildId == guildId && i.DiscordUserId == userId, cancellationToken))
            return false;

        var botName = await embedBranding.GetBotDisplayNameAsync(guildId);
        var opener = BuildOpener(botName);
        var declineButton = new ButtonProperties(DeclineButtonId, "Nein danke", ButtonStyle.Secondary);

        var now = DateTimeOffset.UtcNow;
        var messageId = await notificationDispatcher.SendDirectMessageAsync(userId, opener, declineButton);

        var interview = new MemberInterview
        {
            GuildId = guildId,
            GuildAllianceId = guildAllianceId,
            DiscordUserId = userId,
            Status = messageId is null ? MemberInterviewStatus.Undeliverable : MemberInterviewStatus.Invited,
            InvitedAt = now,
            LastActivityAt = now,
        };
        if (messageId is not null)
            interview.Messages.Add(new MemberInterviewMessage { Role = MemberInterviewRole.Bot, Content = opener, CreatedAt = now });

        db.MemberInterviews.Add(interview);
        await db.SaveChangesAsync(cancellationToken);
        return messageId is not null;
    }

    // Handles a member's DM reply for an active interview. No-op if the user has no active interview
    // (e.g. a random DM, or one already finished).
    public async Task HandleReplyAsync(ulong userId, string content, CancellationToken cancellationToken)
    {
        // The member's most recent interview for this guild, regardless of status — a fresh DM re-opens
        // a previously declined, completed, or undeliverable one (both the decline and completion
        // closers explicitly invite the member to just write again). Only a member who was never
        // invited at all stays unanswered.
        var interview = await db.MemberInterviews
            .Where(i => i.DiscordUserId == userId)
            .OrderByDescending(i => i.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (interview is null)
            return;

        var now = DateTimeOffset.UtcNow;
        db.MemberInterviewMessages.Add(new MemberInterviewMessage
        {
            InterviewId = interview.Id,
            Role = MemberInterviewRole.Member,
            Content = content,
            CreatedAt = now,
        });
        interview.Status = MemberInterviewStatus.InProgress;
        // Re-opening extends the transcript, so clear the closed markers — it can complete (and be
        // re-extracted with the new content) again. No-ops for an already-active interview.
        interview.CompletedAt = null;
        interview.ExtractedAt = null;
        interview.LastActivityAt = now;
        await db.SaveChangesAsync(cancellationToken);

        if (IsOptOut(content))
        {
            await CloseAsync(interview, MemberInterviewStatus.Declined,
                "Alles klar, kein Problem! Falls du es dir später anders überlegst, schreib mir einfach. 🖖", cancellationToken);
            return;
        }

        var transcript = await db.MemberInterviewMessages
            .Where(m => m.InterviewId == interview.Id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        var turns = transcript
            .Select(m => new AiChatTurn(m.Role == MemberInterviewRole.Bot ? AiChatRole.Assistant : AiChatRole.User, m.Content))
            .ToList();

        var botName = await embedBranding.GetBotDisplayNameAsync(interview.GuildId);
        var forceWrapUp = turns.Count(t => t.Role == AiChatRole.User) >= MaxTurns;
        var systemPrompt = BuildInterviewPrompt(botName, forceWrapUp);

        // Interviews are casual, in-character DM chat — use the lighter/cheaper member-lore model
        // (flash-lite by default) so they don't burn the premium answer model's tiny per-day quota.
        var model = await modelResolver.ResolveLightweightAsync(interview.GuildId);
        var answer = await model.Provider.GenerateAsync(
            new AiChatCompletionRequest(model.Model, systemPrompt, turns, model.ApiKey), cancellationToken);
        if (string.IsNullOrWhiteSpace(answer))
        {
            logger.LogWarning("Member interview {InterviewId}: model returned nothing; leaving the reply unanswered.", interview.Id);
            return;
        }

        var done = answer.Contains(DoneSentinel, StringComparison.OrdinalIgnoreCase) || forceWrapUp;
        answer = answer.Replace(DoneSentinel, "", StringComparison.OrdinalIgnoreCase).Trim();

        db.MemberInterviewMessages.Add(new MemberInterviewMessage
        {
            InterviewId = interview.Id,
            Role = MemberInterviewRole.Bot,
            Content = answer,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        if (done)
        {
            interview.Status = MemberInterviewStatus.Completed;
            interview.CompletedAt = DateTimeOffset.UtcNow;
        }
        interview.LastActivityAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await notificationDispatcher.SendDirectMessageAsync(userId, answer);

        if (done)
            await AssignCompletedRoleAsync(interview);
    }

    // Grants the configured "interview completed" role (if any) when a member finishes their interview.
    private async Task AssignCompletedRoleAsync(MemberInterview interview)
    {
        if (interview.GuildAllianceId is not { } guildAllianceId)
            return;

        var roleId = await settingsService.GetSnowflakeAsync(
            interview.GuildId, GuildFeature.MemberLore, GuildAudience.Alliance, guildAllianceId, MemberLoreSettingKeys.CompletedRole);
        if (roleId is not { } role)
            return;

        try
        {
            await gatewayClient.Rest.AddGuildUserRoleAsync(interview.GuildId, interview.DiscordUserId, role);
        }
        catch (RestException ex)
        {
            logger.LogWarning(ex, "Could not grant the interview-completed role {RoleId} to user {UserId} in guild {GuildId}",
                role, interview.DiscordUserId, interview.GuildId);
        }
    }

    // Opt-out via the "Nein danke" button.
    public async Task DeclineAsync(ulong userId, CancellationToken cancellationToken)
    {
        var interview = await db.MemberInterviews
            .Where(i => i.DiscordUserId == userId
                && (i.Status == MemberInterviewStatus.Invited || i.Status == MemberInterviewStatus.InProgress))
            .OrderByDescending(i => i.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (interview is null)
            return;

        await CloseAsync(interview, MemberInterviewStatus.Declined,
            "Kein Problem, ich lasse dich in Ruhe! Falls du mir doch mal etwas erzählen magst, schreib mir einfach. 🖖", cancellationToken);
    }

    private async Task CloseAsync(MemberInterview interview, MemberInterviewStatus status, string closingMessage, CancellationToken cancellationToken)
    {
        interview.Status = status;
        interview.CompletedAt = DateTimeOffset.UtcNow;
        interview.LastActivityAt = DateTimeOffset.UtcNow;
        db.MemberInterviewMessages.Add(new MemberInterviewMessage
        {
            InterviewId = interview.Id,
            Role = MemberInterviewRole.Bot,
            Content = closingMessage,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await notificationDispatcher.SendDirectMessageAsync(interview.DiscordUserId, closingMessage);
    }

    private static bool IsOptOut(string content)
    {
        var c = content.Trim().ToLowerInvariant();
        return c is "nein danke" or "nein, danke" or "stop" or "stopp" or "no thanks" or "kein interesse";
    }

    private static string BuildOpener(string botName) =>
        $"🖖 Hi! Ich bin {botName} und möchte dich gern besser kennenlernen, damit ich ein echtes Mitglied unserer " +
        "Community sein kann. Magst du mir ein bisschen was über dich erzählen? Zum Beispiel: Wie soll ich dich nennen? " +
        "Was machst du so – im Spiel und sonst? Und hast du vielleicht ein paar lustige Geschichten über andere Spieler? 😄\n\n" +
        "Alles völlig freiwillig – wenn du gerade keine Lust hast, klick einfach unten auf den Nein-danke-Knopf, dann lasse ich dich in Ruhe.";

    private static string BuildInterviewPrompt(string botName, bool forceWrapUp)
    {
        var basePrompt =
            HoshiPersona.Describe(botName) + "\n\n" +
            "Du führst gerade ein lockeres, freundliches Kennenlern-Gespräch per Direktnachricht mit einem Mitglied, " +
            "um es besser kennenzulernen. Deine Ziele: Wie das Mitglied genannt werden möchte; was es im Spiel und in der " +
            "Allianz so macht; und ob es lustige oder charmante Geschichten über andere Mitglieder hat.\n\n" +
            "Sei warm, kurz und bleibe in deiner Rolle. Stelle immer nur eine, höchstens zwei Fragen auf einmal und gehe " +
            "echt auf die Antworten ein. Sei ruhig neugierig und hake mit interessierten Nachfragen nach – zu Spiel, " +
            "Rolle in der Allianz und gemeinsamen Erlebnissen; genau dafür mögen dich die Leute. Bei privaten oder " +
            "persönlichen Themen sei dagegen zurückhaltend: frage nicht aktiv nach, sondern nur, wenn das Mitglied von " +
            "selbst darüber erzählt. Dränge zu nichts und respektiere immer, wenn jemand etwas nicht teilen möchte.\n\n" +
            "WICHTIG: Antworte immer in derselben Sprache, in der das Mitglied schreibt (Deutsch, Englisch, …).";

        var wrapUp = forceWrapUp
            ? " Das Gespräch ist jetzt lang genug: Bedanke dich herzlich, sag, dass es dir jederzeit mehr erzählen kann, und beende es."
            : " Beende das Gespräch nicht zu früh – plaudere ruhig ein bisschen und stelle noch ein, zwei interessierte " +
              "Nachfragen (zu Spiel, Allianz, gemeinsamen Erlebnissen), bevor du abschließt. Schließe erst ab, wenn das " +
              "Mitglied sich verabschieden bzw. abschließen möchte oder ihr wirklich ausführlich geplaudert habt; bedanke " +
              "dich dann herzlich und sag, dass es dir jederzeit mehr erzählen kann.";

        return basePrompt + wrapUp +
            $"\n\nWenn (und nur wenn) du das Gespräch beendest, schreibe GANZ AM ENDE deiner Nachricht auf einer eigenen " +
            $"Zeile exakt {DoneSentinel} — das Mitglied sieht diesen Marker nicht.";
    }
}
