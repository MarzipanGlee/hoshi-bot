using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Discord.Permissions;
using HoshiBot.Discord.ReadReceipts;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Boarding;

// The welcome message a new member confirms to get in, and the two roles that move around it.
//
// Three jobs, deliberately separate:
//
//   RefreshMessageAsync — the standing post in the boarding channel. Written once by an admin,
//   edited in place afterwards, exactly like the announcement draft hub.
//
//   BoardAsync — a member arrives, gets the boarding role and (optionally) a DM pointing at that
//   post. Called once per member ever; BoardingEntry is what guarantees the "once".
//
//   OnConfirmedAsync — they pressed the button. Member role on, boarding role off, DM tidied away.
//   Reached through IReadConfirmationFollowUp, so the button itself is the ordinary read-receipt
//   button and every path that renders one (the post, the unread list, the counter job) keeps
//   working without knowing Boarding exists.
public class BoardingService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    EmbedBranding embedBranding,
    GuildFeatureSettingsService settingsService,
    BoardingRoles boardingRoles,
    MemberRoles memberRoles,
    ReadReceiptService readReceipts,
    LanguageResolver languageResolver,
    NotificationDispatcher dispatcher,
    ChannelCooldown cooldown,
    PermissionGuard permissionGuard,
    ILogger<BoardingService> logger) : IReadConfirmationFollowUp
{
    public ReadablePostKind Kind => ReadablePostKind.WelcomeMessage;

    // ---- The standing message -------------------------------------------------------------

    // Posts the boarding message, or edits the one already there. Returns false when the scope is
    // not configured enough to post — the caller (the Publish queue) turns that into a message the
    // admin can act on rather than a silent no-op.
    public async Task<bool> RefreshMessageAsync(ulong guildId, GuildAudience audience, int? guildAllianceId,
        CancellationToken cancellationToken = default)
    {
        var channelId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Boarding, audience, guildAllianceId, BoardingSettingKeys.Channel);
        var message = await settingsService.GetTextAsync(guildId, GuildFeature.Boarding, audience, guildAllianceId, BoardingSettingKeys.Message);
        var memberRoleId = await memberRoles.ForScopeAsync(guildId, audience, guildAllianceId);
        var boardingRoleId = await boardingRoles.ForScopeAsync(guildId, audience, guildAllianceId);

        // Refuse rather than post half a feature: a welcome message whose button cannot grant a role
        // is worse than no message, because a member presses it and nothing happens.
        if (channelId is not { } boardingChannelId || string.IsNullOrWhiteSpace(message)
            || memberRoleId is null || boardingRoleId is null)
        {
            return false;
        }

        if (cooldown.IsCoolingDown(boardingChannelId, BotAction.RefreshBoardingMessage))
            return false;

        var lang = await languageResolver.ForScopeAsync(guildId, audience, guildAllianceId);
        var label = await settingsService.GetTextAsync(guildId, GuildFeature.Boarding, audience, guildAllianceId, BoardingSettingKeys.ButtonLabel);
        var embed = await embedBranding.BuildBrandedAsync(guildId, message, title: Msg.Boarding.Title(lang));

        var storedId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Boarding, audience, guildAllianceId, BoardingSettingKeys.MessageId);

        if (storedId is { } messageId)
        {
            var existing = await db.ReadablePosts.FirstOrDefaultAsync(p => p.GuildId == guildId && p.MessageId == messageId, cancellationToken);
            try
            {
                // Re-stamp the caption before rendering: an admin who changed it expects the button
                // to change with the text, and every other renderer reads it off this row.
                if (existing is not null)
                {
                    existing.ButtonLabel = Blank(label);
                    await db.SaveChangesAsync(cancellationToken);
                }

                await gatewayClient.Rest.ModifyMessageAsync(boardingChannelId, messageId, m =>
                {
                    m.Embeds = [embed];
                    if (existing is not null)
                        m.Components = [ReadReceiptService.Buttons(existing, existing.LastKnownReadCount)];
                }, cancellationToken: cancellationToken);

                cooldown.RecordSuccess(boardingChannelId, BotAction.RefreshBoardingMessage);
                return true;
            }
            catch (RestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Genuinely gone — someone cleared the channel. Fall through and post a fresh one.
            }
            catch (RestException ex)
            {
                // Anything else must NOT re-post: a rate limit or a hiccup would leave the live
                // message orphaned beside a duplicate, and members would hold buttons on both. Same
                // rule AbsenceService and the draft hub document.
                await HandleFailureAsync(guildId, boardingChannelId, ex, "edit");
                return false;
            }
        }

        try
        {
            // Posted bare: the button's custom id needs the ReadablePost row's id, which does not
            // exist until the message does.
            var posted = await gatewayClient.Rest.SendMessageAsync(boardingChannelId, new MessageProperties
            {
                Embeds = [embed],
                Flags = MessageFlags.SuppressNotifications,
            }, cancellationToken: cancellationToken);

            var post = await readReceipts.RegisterAsync(guildId, boardingChannelId, posted.Id, ReadablePostKind.WelcomeMessage,
                audience, guildAllianceId, Msg.Boarding.Title(lang), lang, Blank(label), cancellationToken);

            // Note the button goes on regardless of post.ReadReceiptsEnabled. That flag decides
            // whether the post joins the unread list and the counter job — but here the button IS
            // the feature, and a boarding message without one cannot be confirmed at all.
            await gatewayClient.Rest.ModifyMessageAsync(boardingChannelId, posted.Id,
                m => m.Components = [ReadReceiptService.Buttons(post, 0)], cancellationToken: cancellationToken);

            cooldown.RecordSuccess(boardingChannelId, BotAction.RefreshBoardingMessage);
            await settingsService.SetSnowflakeAsync(guildId, GuildFeature.Boarding, audience, guildAllianceId, BoardingSettingKeys.MessageId, posted.Id);

            // Old entries keep pointing at the previous row, which is correct: they were boarded
            // against that message and their receipts live there.
            return true;
        }
        catch (RestException ex)
        {
            await HandleFailureAsync(guildId, boardingChannelId, ex, "post");
            return false;
        }
    }

    // ---- Boarding a member ----------------------------------------------------------------

    // Gives a member the boarding role and points them at the message. Does nothing — and says so by
    // returning false — if they have been boarded before, whatever came of it. That is the whole
    // guard against re-boarding someone who confirmed and later lost the member role for unrelated
    // reasons, which would otherwise repeat every time the job ran.
    public async Task<bool> BoardAsync(ulong guildId, GuildUser member, BoardingScopes.Scope scope, bool sendDm,
        CancellationToken cancellationToken = default)
    {
        if (await db.BoardingEntries.AnyAsync(e => e.GuildId == guildId && e.DiscordUserId == member.Id, cancellationToken))
            return false;

        if (await boardingRoles.ForScopeAsync(guildId, scope.Audience, scope.GuildAllianceId) is not { } boardingRoleId)
            return false;

        var post = await FindPostAsync(guildId, scope, cancellationToken);
        if (post is null)
            return false;

        // Already a member — nothing to board them into. Recording the entry anyway stops the job
        // reconsidering them on every pass.
        var memberRoleId = await memberRoles.ForScopeAsync(guildId, scope.Audience, scope.GuildAllianceId);
        if (memberRoleId is { } existingMemberRole && member.RoleIds.Contains(existingMemberRole))
        {
            db.BoardingEntries.Add(NewEntry(guildId, member.Id, post.Id, BoardingStatus.Confirmed, confirmed: true));
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        if (!await TryAddRoleAsync(guildId, member, boardingRoleId, cancellationToken))
            return false;

        ulong? dmMessageId = null;
        var status = BoardingStatus.Boarded;

        if (sendDm)
        {
            var lang = await languageResolver.ForScopeAsync(guildId, scope.Audience, scope.GuildAllianceId);
            var text = await settingsService.GetTextAsync(guildId, GuildFeature.Boarding, scope.Audience, scope.GuildAllianceId, BoardingSettingKeys.WelcomeDm);

            // Blank means no DM at all, which is the default — see CONTRIBUTING's member-messaging
            // rule and the carve-out written beside it.
            if (!string.IsNullOrWhiteSpace(text))
            {
                var body = text
                    .Replace(CommanderPlaceholder, CommanderName.Of(member))
                    .Replace(LinkPlaceholder, MessageLink(guildId, post));

                dmMessageId = await dispatcher.SendDirectMessageAsync(member.Id, body);
                if (dmMessageId is null)
                    status = BoardingStatus.Undeliverable;
            }
        }

        db.BoardingEntries.Add(NewEntry(guildId, member.Id, post.Id, status, confirmed: false, dmMessageId));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Confirming -----------------------------------------------------------------------

    // Runs on every click of the welcome message's button, including one where the receipt already
    // existed — see IReadConfirmationFollowUp. Everything below is decided from the member's actual
    // roles, so a click that failed halfway last time finishes this time.
    public async Task<string?> OnConfirmedAsync(ReadablePost post, GuildUser member, Language lang)
    {
        if (await memberRoles.ForScopeAsync(post.GuildId, post.Audience, post.GuildAllianceId) is not { } memberRoleId)
            return null;

        var entry = await db.BoardingEntries.FirstOrDefaultAsync(e => e.GuildId == post.GuildId && e.DiscordUserId == member.Id);

        // Member role FIRST. If this fails the member keeps the boarding role and still reads as
        // not-done, which is the safe direction: the reverse order would leave someone with no role
        // at all and no way to tell.
        if (!member.RoleIds.Contains(memberRoleId) && !await TryAddRoleAsync(post.GuildId, member, memberRoleId))
        {
            if (entry is not null)
            {
                entry.Status = BoardingStatus.RoleGrantFailed;
                await db.SaveChangesAsync();
            }

            return Msg.Boarding.RoleFailed(lang);
        }

        if (await boardingRoles.ForScopeAsync(post.GuildId, post.Audience, post.GuildAllianceId) is { } boardingRoleId
            && member.RoleIds.Contains(boardingRoleId))
            await TryRemoveRoleAsync(post.GuildId, member, boardingRoleId);

        if (entry is not null)
        {
            if (entry.DmMessageId is { } dmMessageId)
            {
                await dispatcher.DeleteDirectMessageAsync(member.Id, dmMessageId);
                entry.DmMessageId = null;
            }

            entry.Status = BoardingStatus.Confirmed;
            entry.ConfirmedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        return Msg.Boarding.Welcome(lang);
    }

    // ---- Shared -----------------------------------------------------------------------------

    public Task<ReadablePost?> FindPostAsync(ulong guildId, BoardingScopes.Scope scope, CancellationToken cancellationToken = default) =>
        db.ReadablePosts
            .Where(p => p.GuildId == guildId
                && p.Kind == ReadablePostKind.WelcomeMessage
                && p.Audience == scope.Audience
                && p.GuildAllianceId == scope.GuildAllianceId)
            .OrderByDescending(p => p.PostedAt)
            .FirstOrDefaultAsync(cancellationToken);

    // What a member types into the DM text to get their own name and a jump link to the message.
    public const string CommanderPlaceholder = "{commander}";
    public const string LinkPlaceholder = "{link}";

    private static string MessageLink(ulong guildId, ReadablePost post) =>
        $"https://discord.com/channels/{guildId}/{post.ChannelId}/{post.MessageId}";

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static BoardingEntry NewEntry(ulong guildId, ulong userId, int postId, BoardingStatus status,
        bool confirmed, ulong? dmMessageId = null) => new()
        {
            GuildId = guildId,
            DiscordUserId = userId,
            ReadablePostId = postId,
            DmMessageId = dmMessageId,
            Status = status,
            BoardedAt = DateTimeOffset.UtcNow,
            ConfirmedAt = confirmed ? DateTimeOffset.UtcNow : null,
        };

    private async Task<bool> TryAddRoleAsync(ulong guildId, GuildUser member, ulong roleId, CancellationToken cancellationToken = default)
    {
        // A definite no from the cache saves the round trip — and, on a backfill, saves hundreds of
        // 403s that would count toward Discord's invalid-request ban. Null means "couldn't tell",
        // and then we try anyway.
        if (permissionGuard.For(guildId) is { } perms && (!perms.CanManageRoles || !perms.CanAssign(roleId)))
        {
            await NotifyRoleProblemAsync(guildId, "the bot's own role is not above it, or it lacks Manage Roles");
            return false;
        }

        try
        {
            await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, member.Id, roleId, cancellationToken: cancellationToken);
            NotificationDispatcher.ClearPermissionIssue(guildId, BotAction.BoardMember, null);
            return true;
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await NotifyRoleProblemAsync(guildId, $"{(int)ex.StatusCode} adding role {roleId}");
            return false;
        }
    }

    private async Task TryRemoveRoleAsync(ulong guildId, GuildUser member, ulong roleId)
    {
        try
        {
            await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, member.Id, roleId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            // They keep the boarding role alongside the member role — untidy, not broken, and the
            // sync job retries it. Not worth telling the member their confirmation failed when it
            // did not.
            logger.LogInformation("Could not remove boarding role {RoleId} in guild {GuildId}: {StatusCode}", roleId, guildId, ex.StatusCode);
        }
    }

    private Task NotifyRoleProblemAsync(ulong guildId, string reason)
    {
        logger.LogWarning("Boarding could not move roles in guild {GuildId}: {Reason}", guildId, reason);

        // Throttled per (guild, action), so a backfill of 900 members produces one admin ping rather
        // than 900.
        return dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.BoardMember, null, BotPermission.ManageRoles);
    }

    private async Task HandleFailureAsync(ulong guildId, ulong channelId, RestException ex, string what)
    {
        cooldown.RecordFailure(channelId, BotAction.RefreshBoardingMessage);
        logger.LogWarning(ex, "Could not {What} the boarding message in channel {ChannelId} for guild {GuildId}", what, channelId, guildId);

        if (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.RefreshBoardingMessage, channelId, ChannelAccessProfile.Post.Permissions());
    }
}
