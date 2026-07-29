using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.StfcNews;

public enum StfcNewsActionOutcome { NotFound, AlreadyResolved, CannotConfirmOwnSubmission, Submitted, Confirmed, Resolved }

// Core logic for the crowd-sourced event-date confirmation flow — see StfcNewsNotifyJob for
// detection/initial send, StfcNewsButtonModule/StfcNewsModalModule for the Discord-facing
// entry points, StfcNewsStatsRefreshJob for the batched "X confirmed" display catch-up.
//
// The interaction's own response is a personal ephemeral reply built by the caller from the
// outcome returned here — it is never the same message as the shared post embed, so updating
// the shared message (via RefreshAllGuildMessagesAsync, below) can always go through a plain
// direct REST edit with no risk of conflicting with the interaction's own response.
public class StfcNewsService(HoshiBotDbContext db, GatewayClient gatewayClient, LanguageResolver languageResolver)
{
    public async Task<StfcNewsActionOutcome> SubmitDateAsync(int postId, DateOnly date, ulong submitterId)
    {
        var post = await db.StfcNewsPosts
            .Include(p => p.GuildMessages)
            .FirstOrDefaultAsync(p => p.Id == postId);
        if (post is null)
            return StfcNewsActionOutcome.NotFound;
        if (post.ResolvedAt is not null)
            return StfcNewsActionOutcome.AlreadyResolved;

        post.SubmittedDate = date;
        post.SubmittedByDiscordUserId = submitterId;
        post.SubmittedAt = DateTimeOffset.UtcNow;

        // A new date needs a fresh quorum — any confirmations against the previous date no
        // longer mean anything.
        var staleConfirmations = await db.StfcEventDateConfirmations
            .Where(c => c.StfcNewsPostId == postId)
            .ToListAsync();
        db.StfcEventDateConfirmations.RemoveRange(staleConfirmations);

        await db.SaveChangesAsync();

        // A submission is a one-time event, not a hot loop — update every guild's message
        // right away rather than waiting for the batched stats refresh.
        await RefreshAllGuildMessagesAsync(post);
        return StfcNewsActionOutcome.Submitted;
    }

    // The submitter's own click never counts as a confirmation — otherwise a single admin
    // could unilaterally submit and immediately self-confirm, defeating the point of a
    // crowd-sourced quorum.
    public async Task<(StfcNewsActionOutcome Outcome, int ConfirmationCount, int RequiredConfirmations)> ConfirmDateAsync(
        int postId, ulong callerId)
    {
        var post = await db.StfcNewsPosts
            .Include(p => p.GuildMessages)
            .FirstOrDefaultAsync(p => p.Id == postId);
        if (post is null || post.SubmittedDate is null)
            return (StfcNewsActionOutcome.NotFound, 0, 0);
        if (post.ResolvedAt is not null)
            return (StfcNewsActionOutcome.AlreadyResolved, 0, 0);
        if (post.SubmittedByDiscordUserId == callerId)
            return (StfcNewsActionOutcome.CannotConfirmOwnSubmission, 0, 0);

        var alreadyConfirmed = await db.StfcEventDateConfirmations
            .AnyAsync(c => c.StfcNewsPostId == postId && c.DiscordUserId == callerId);
        if (!alreadyConfirmed)
        {
            db.StfcEventDateConfirmations.Add(new StfcEventDateConfirmation
            {
                StfcNewsPostId = postId,
                DiscordUserId = callerId,
                ConfirmedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var count = await db.StfcEventDateConfirmations.CountAsync(c => c.StfcNewsPostId == postId);
        var isTrusted = await db.TrustedUsers.AnyAsync(u => u.DiscordUserId == callerId);

        if (isTrusted || count >= post.RequiredConfirmations)
        {
            await ResolveAsync(post);
            await RefreshAllGuildMessagesAsync(post);
            return (StfcNewsActionOutcome.Resolved, count, post.RequiredConfirmations);
        }

        // Not resolved yet — deliberately does NOT refresh the shared messages here (that's
        // what makes ordinary confirm clicks safe under rate limits; the periodic
        // StfcNewsStatsRefreshJob catches everyone up every 5 minutes). The confirming admin
        // still gets fresh personal feedback via their own ephemeral reply, built by the
        // caller from the count/required returned here.
        return (StfcNewsActionOutcome.Confirmed, count, post.RequiredConfirmations);
    }

    // Branches on EventGroup: Infinite Incursions is region-split (3 StfcEventStatus rows),
    // everything else (currently only Alliance Tournament) is a single universal row.
    private async Task ResolveAsync(StfcNewsPost post)
    {
        if (post.EventGroup == "incursions")
        {
            var regionRows = await db.StfcEventStatuses
                .Where(e => e.EventGroup == "incursions")
                .ToListAsync();
            var regionDefaults = await db.IncursionsRegionDefaults.ToDictionaryAsync(d => d.RegionId);
            var settings = await db.StfcNewsSettings.FindAsync(1) ?? new StfcNewsSettings { Id = 1 };

            foreach (var row in regionRows)
            {
                if (row.RegionId is not { } regionId || !regionDefaults.TryGetValue(regionId, out var regionDefault))
                    continue;

                var eventStart = new DateTimeOffset(post.SubmittedDate!.Value.ToDateTime(regionDefault.DefaultStartTimeUtc), TimeSpan.Zero);
                row.EventStart = eventStart;
                row.EventEnd = eventStart + TimeSpan.FromHours(settings.IncursionsEventDurationHours);
                row.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        else
        {
            var row = await db.StfcEventStatuses.FirstOrDefaultAsync(e => e.EventGroup == post.EventGroup);
            if (row is not null)
            {
                var existingTime = TimeOnly.FromDateTime(row.EventStart.UtcDateTime);
                row.EventStart = new DateTimeOffset(post.SubmittedDate!.Value.ToDateTime(existingTime), TimeSpan.Zero);
                row.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        post.ResolvedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    // Recomputes the live confirmation count and edits every guild's copy of the shared
    // message to match, updating LastDisplayedConfirmationCount so the periodic stats job can
    // tell what's already been shown.
    public async Task RefreshAllGuildMessagesAsync(StfcNewsPost post)
    {
        var count = await db.StfcEventDateConfirmations.CountAsync(c => c.StfcNewsPostId == post.Id);

        // Each guild's copy of the shared message renders in that guild's language (the post
        // goes to the guild-scoped AdminChannelId) — built once per distinct language.
        var rendered = new Dictionary<Language, (EmbedProperties Embed, IReadOnlyList<ButtonProperties>? Buttons)>();

        foreach (var guildMessage in post.GuildMessages)
        {
            var lang = await languageResolver.ForGuildAsync(guildMessage.GuildId);
            if (!rendered.TryGetValue(lang, out var r))
                rendered[lang] = r = StfcNewsMessageBuilder.Build(post, count, lang);
            var (embed, buttons) = r;

            try
            {
                await gatewayClient.Rest.ModifyMessageAsync(guildMessage.ChannelId, guildMessage.MessageId, m =>
                {
                    m.Embeds = [embed];
                    m.Components = buttons is null ? [] : [new ActionRowProperties(buttons)];
                });
            }
            catch (RestException)
            {
                // Channel/message gone or permission revoked since it was sent — skip, same
                // best-effort tolerance NotificationDispatcher's own edits use.
            }
        }

        post.LastDisplayedConfirmationCount = count;
        await db.SaveChangesAsync();
    }
}
