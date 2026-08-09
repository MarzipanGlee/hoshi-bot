using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
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
    LanguageResolver languageResolver,
    InterviewOpener interviewOpener,
    PlayerLinkService playerLinkService,
    ILogger<MemberInterviewService> logger)
{
    public const string DeclineButtonId = "member-interview-decline";

    // The decline button and the closers render in the interviewee's resolved language from the
    // catalog; the opener is translated on the fly from an English constant (InterviewOpener), and
    // the interview conversation itself mirrors the member's language via the LLM prompt below.

    // The model appends this on its own line when it has learned enough; stripped before sending.
    private const string DoneSentinel = "[INTERVIEW_DONE]";

    // Safety cap so an interview can't run forever; on the last turn the model is told to wrap up.
    private const int MaxTurns = 14;

    // Sends the opener DM and creates the interview. Returns true if the DM went through (counts
    // against the daily budget); false if the member's DMs are closed (recorded Undeliverable) or an
    // interview already exists.
    public async Task<bool> InviteAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, ulong userId, CancellationToken cancellationToken)
    {
        if (await db.MemberInterviews.AnyAsync(i => i.GuildId == guildId && i.DiscordUserId == userId, cancellationToken))
            return false;

        var lang = await languageResolver.ForUserAsync(userId, scopeGuildId: guildId);
        var botName = await ResolveBotNameAsync(guildId);
        var (allianceName, _) = await ResolveAllianceAsync(guildId, guildAllianceId, cancellationToken);
        // The lighter/cheaper member-lore model, same as the conversation itself uses below.
        var opener = await interviewOpener.RenderAsync(
            await modelResolver.ResolveLightweightAsync(guildId), botName, allianceName, lang, cancellationToken);
        var declineButton = new ButtonProperties(DeclineButtonId, Msg.Interview.DeclineButton(lang), ButtonStyle.Secondary);

        var now = DateTimeOffset.UtcNow;
        var messageId = await notificationDispatcher.SendDirectMessageAsync(userId, opener, declineButton);

        var interview = new MemberInterview
        {
            GuildId = guildId,
            Audience = audience,
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

    // Her display name without the alliance tag her guild nickname carries. "[LF] Hoshi Sato,
    // communication officer of Lost Falcons" says the alliance twice; "Hoshi Sato, communication
    // officer of Lost Falcons" is what she'd actually say. Same strip the player matcher and the
    // "Commander {name}" greetings use, so it handles multiple tags too.
    private async Task<string> ResolveBotNameAsync(ulong guildId)
    {
        var displayName = await embedBranding.GetBotDisplayNameAsync(guildId);
        var stripped = NicknameComposer.Strip(displayName).Trim();
        return stripped.Length == 0 ? displayName : stripped;
    }

    // The alliance Hoshi introduces herself for, and speaks for during the interview. The Name column
    // is the plain name — the tag lives in its own column — so this is already "without the tag".
    // MemberLore is Alliance-audience, so the link is always there in practice; the Discord guild's
    // own name and then a generic noun are belt-and-braces fallbacks that keep the sentence intact.
    // StfcAllianceId comes along because the prompt needs it to tell members from guests.
    private async Task<(string Name, int? StfcAllianceId)> ResolveAllianceAsync(ulong guildId, int? guildAllianceId, CancellationToken cancellationToken)
    {
        var link = guildAllianceId is not { } linkId
            ? null
            : await db.GuildAlliances
                .Where(ga => ga.Id == linkId)
                .Select(ga => new { ga.StfcAlliance.Name, ga.StfcAllianceId })
                .FirstOrDefaultAsync(cancellationToken);

        if (link is not null && !string.IsNullOrWhiteSpace(link.Name))
            return (link.Name, link.StfcAllianceId);

        var guildName = gatewayClient.Cache.Guilds.GetValueOrDefault(guildId)?.Name;
        return (string.IsNullOrWhiteSpace(guildName) ? "the alliance" : guildName, link?.StfcAllianceId);
    }

    // Who Hoshi is actually talking to — the opening paragraph of the interview prompt, including
    // the goals, because those differ per case.
    //
    // The invite job's gate is a Discord *role*, and guilds hand that role to friends from other
    // alliances too (confirmed: two invitees in Lost Falcons play in KW and IRS, one of them on a
    // different server entirely). So "was invited" is not "plays here", and the prompt used to
    // assert the latter — leaving Hoshi asking a guest what they get up to in an alliance they
    // aren't in. Their player link is what actually knows, when there is one.
    private async Task<string> DescribeSubjectAsync(MemberInterview interview, string allianceName, int? homeAllianceId, CancellationToken cancellationToken)
    {
        const string Goals = "Your goals: what they want to be called; ";

        var playerId = await playerLinkService.GetGuildPrimaryPlayerIdAsync(interview.GuildId, interview.DiscordUserId);
        var player = playerId is not { } pid
            ? null
            : await db.StfcPlayers
                .Where(p => p.Id == pid)
                .Select(p => new
                {
                    p.Name,
                    p.AllianceId,
                    AllianceLabel = p.Alliance != null ? p.Alliance.Name : null,
                    AllianceTag = p.Alliance != null ? p.Alliance.Tag : null,
                    Server = p.Server.Region.Name + p.ServerId,
                })
                .FirstOrDefaultAsync(cancellationToken);

        // No link at all: the bot genuinely doesn't know, and guessing "member" is what caused this
        // in the first place — so say so and let the person answer it themselves.
        if (player is null)
        {
            return $"You are having a relaxed, friendly get-to-know-you conversation by direct message with someone " +
                $"from the \"{allianceName}\" Discord. You do NOT know whether they actually play in {allianceName} — " +
                "some people here are guests from other alliances — so never assume it; let them tell you. " +
                Goals + "which alliance they play in and what they get up to in the game; and whether they have any " +
                "funny or charming stories about other people in this community.";
        }

        var isMember = homeAllianceId is not { } home || player.AllianceId == home;
        if (isMember)
        {
            return $"You are having a relaxed, friendly get-to-know-you conversation by direct message with a member of " +
                $"the alliance \"{allianceName}\", to get to know them better. They play as \"{player.Name}\". " +
                Goals + "what they get up to in the game and in the alliance; and whether they have any funny or " +
                "charming stories about other members.";
        }

        // A linked player with no alliance at all lands here too (roster data has them unaffiliated),
        // hence the second phrasing — "in the alliance another alliance" is the sentence to avoid.
        var theirAlliance = player.AllianceLabel is { Length: > 0 } label
            ? $"in the alliance \"{label}\"{(player.AllianceTag is { Length: > 0 } tag ? $" [{tag}]" : "")}"
            : "and are currently in no alliance";

        return $"You are having a relaxed, friendly get-to-know-you conversation by direct message with a GUEST of the " +
            $"\"{allianceName}\" Discord — they are NOT a member of {allianceName}, so never imply that they are. They " +
            $"play as \"{player.Name}\" {theirAlliance}, on server {player.Server}. They know this " +
            "community from voice chat and from visiting, which is exactly why they are worth talking to. " +
            Goals + "what they get up to in the game and in their own alliance; how they came to know this community; " +
            $"and whether they have any funny or charming stories about the people in {allianceName}.";
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
                Msg.Interview.OptOutClose(await languageResolver.ForUserAsync(userId, scopeGuildId: interview.GuildId)), cancellationToken);
            return;
        }

        var transcript = await db.MemberInterviewMessages
            .Where(m => m.InterviewId == interview.Id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        var turns = transcript
            .Select(m => new AiChatTurn(m.Role == MemberInterviewRole.Bot ? AiChatRole.Assistant : AiChatRole.User, m.Content))
            .ToList();

        var botName = await ResolveBotNameAsync(interview.GuildId);
        var (allianceName, homeAllianceId) = await ResolveAllianceAsync(interview.GuildId, interview.GuildAllianceId, cancellationToken);
        var subject = await DescribeSubjectAsync(interview, allianceName, homeAllianceId, cancellationToken);
        var forceWrapUp = turns.Count(t => t.Role == AiChatRole.User) >= MaxTurns;
        var systemPrompt = BuildInterviewPrompt(botName, subject, forceWrapUp);

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
        // The interview's own scope, recorded when it was created. Reading the Alliance audience
        // unconditionally worked while that was the only one Member Lore had; now a server or
        // community interview would look in a scope that has no settings.
        var roleId = await settingsService.GetSnowflakeAsync(
            interview.GuildId, GuildFeature.MemberLore, interview.Audience, interview.GuildAllianceId, MemberLoreSettingKeys.CompletedRole);
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
            Msg.Interview.DeclineClose(await languageResolver.ForUserAsync(userId, scopeGuildId: interview.GuildId)), cancellationToken);
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

    // English, unlike the German HoshiPersona block it's prepended to — the persona is shared with
    // AiChat and /hoshi-say, so translating it belongs in its own change (docs/backlog.md). The mix
    // is fine for the model: the language the member actually reads is pinned by the rule at the end.
    private static string BuildInterviewPrompt(string botName, string subject, bool forceWrapUp)
    {
        var basePrompt =
            HoshiPersona.Describe(botName) + "\n\n" +
            subject + "\n\n" +
            "Be warm and brief, and stay in character. Ask only one question at a time, two at most, and genuinely " +
            "engage with the answers. Do be curious and follow up with interested questions – about the game, their " +
            "role in the alliance, and shared experiences; that is exactly what people like about you. With private or " +
            "personal topics, be reserved instead: don't ask about them, only follow up if the member brings them up " +
            "themselves. Never push, and always respect it when someone doesn't want to share something.\n\n" +
            "IMPORTANT: always answer in the same language the member writes in (German, English, …). " +
            "You are female: wherever that language marks gender, use the feminine form for yourself " +
            "(German \"Kommunikationsoffizierin\", not \"Kommunikationsoffizier\").";

        var wrapUp = forceWrapUp
            ? " The conversation is long enough now: thank them warmly, tell them they can always tell you more, and end it."
            : " Don't end the conversation too early – chat a little and ask another one or two interested follow-up " +
              "questions (about the game, the alliance, shared experiences) before you wrap up. Only wrap up once the " +
              "member wants to say goodbye or finish, or you have really chatted at length; then thank them warmly and " +
              "tell them they can always tell you more.";

        return basePrompt + wrapUp +
            $"\n\nIf (and only if) you end the conversation, write exactly {DoneSentinel} on its own line at the VERY " +
            $"END of your message — the member does not see this marker.";
    }
}
