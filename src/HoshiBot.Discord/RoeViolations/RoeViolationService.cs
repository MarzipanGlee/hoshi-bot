using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.RoeViolations;

// Posts as a forum thread (the configured channel is a Forum channel — see
// RoeViolationReportsEditor's ChannelKind.Forum picker) rather than a private thread under a
// text channel like TicketService: a Forum channel's "create thread" endpoint requires the
// starter message up front (CreateForumGuildThreadAsync), it can't be created empty and filled
// in after the way CreateGuildThreadAsync works for a normal text channel. Otherwise mirrors
// TicketService's shape — same permission-catch-and-notify-admin pattern via
// NotificationDispatcher.
public class RoeViolationService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    NotificationDispatcher dispatcher,
    EmbedBranding embedBranding,
    GuildFeatureSettingsService settingsService,
    GuildAllianceService allianceService,
    PlayerLinkService playerLinkService,
    LanguageResolver languageResolver)
{
    // Reporter-addressed instructions for the forum post. The steps are a separate catalog key
    // spliced into the template so the report's own alliance diplomat role can be mentioned
    // inline — matching the legacy post's format.
    private static string BuildInstructions(Language lang, string reporterName, bool reporterIsVictim, string diplomatMention) =>
        Msg.Roe.Instructions(lang, reporterName,
            reporterIsVictim ? Msg.Roe.VictimSteps(lang) : Msg.Roe.OffenderSteps(lang),
            diplomatMention);

    public static ButtonProperties ReadyButton(int reportId, Language lang) =>
        new($"roe-violation-ready:{reportId}", Msg.Roe.ReadyButton(lang), EmojiProperties.Standard("✅"), ButtonStyle.Success);

    public static ButtonProperties DoneButton(int reportId, Language lang) =>
        new($"roe-violation-done:{reportId}", Msg.Roe.DoneButton(lang), EmojiProperties.Standard("❌"), ButtonStyle.Danger);

    // Shared by both entry points that open this modal (CommandBridgeButtonModule for the
    // to/from branches, RoeViolationUserMenuModule for the other branch) — same 2-field
    // shape regardless of branch, only the custom-id differs. "otherUserId" is an unused
    // placeholder for the to/from branches, keeping the positional custom-id parameters
    // consistent across all 3 branches (same convention already used for Raid's
    // location-in-custom-id).
    public static ModalProperties Modal(string branch, ulong otherUserId, Language lang) =>
        new($"roe-violation-modal:{branch}:{otherUserId}", Msg.Roe.ModalTitle(lang),
        [
            new LabelProperties(Msg.Roe.ModalTagLabel(lang),
                new TextInputProperties("tag", TextInputStyle.Short) { Placeholder = Msg.Roe.ModalTagPlaceholder(lang), Required = true, MaxLength = 10 }),
            new LabelProperties(Msg.Roe.ModalNameLabel(lang),
                new TextInputProperties("name", TextInputStyle.Short) { Placeholder = Msg.Roe.ModalNamePlaceholder(lang), Required = true }),
        ]);

    // Gates the staff-only "report on behalf of an own player" option — the guild-wide Command
    // Staff role (the per-alliance Diplomat role is only for the ready-for-diplomat ping).
    public async Task<bool> IsCommandStaffAsync(ulong guildId, ulong userId)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.CommandStaffRoleId is not { } roleId)
            return false;

        var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
        return guildUser.RoleIds.Contains(roleId);
    }

    // Resolves "this Discord user's current in-game identity" from real linked-player
    // data instead of hardcoding a single alliance tag like legacy does — the player that
    // represents them in this guild, falling back to the caller-supplied display name +
    // "-" tag if the member has no linked player at all.
    public async Task<(string Tag, string Name)> ResolveIdentityAsync(ulong guildId, ulong userId, string fallbackDisplayName)
    {
        var playerId = await playerLinkService.GetGuildPrimaryPlayerIdAsync(guildId, userId);
        if (playerId is null)
            return ("-", fallbackDisplayName);

        var player = await db.StfcPlayers
            .Where(p => p.Id == playerId)
            .Select(p => new { p.Name, AllianceTag = p.Alliance != null ? p.Alliance.Tag : null })
            .FirstOrDefaultAsync();

        return player is null ? ("-", fallbackDisplayName) : (player.AllianceTag ?? "-", player.Name);
    }

    // Branded confirmation embed shown back to the reporter after the modal — matches the rest
    // of the bot's author/footer styling (the legacy report post did the same). Public so the
    // modal module's fallback branch can build one too.
    public Task<EmbedProperties> ResultEmbedAsync(ulong guildId, string title, string description) =>
        embedBranding.BuildBrandedAsync(guildId, description, title: title);

    public async Task<EmbedProperties> CreateReportAsync(ulong guildId, ulong reporterId, string reporterDisplayName, string attackerTag, string attackerName,
        string defenderTag, string defenderName, ulong? attackerDiscordUserId, bool reporterIsVictim)
    {
        // The reporter is both the acting user (ephemeral result embeds) and the person the
        // forum post is dedicated to, so one resolved language covers everything rendered
        // here; only the admin notifications below use the guild language instead.
        var lang = await languageResolver.ForUserAsync(reporterId, scopeGuildId: guildId);

        // The report belongs to the reporter's own linked alliance; if they have no resolvable
        // alliance, fall back to the guild's primary link so a report is never lost.
        var guildAllianceId = (await allianceService.FindByMemberAsync(guildId, reporterId))?.Id
            ?? await allianceService.GetPrimaryIdAsync(guildId);
        var channelIdResult = guildAllianceId is null
            ? null
            : await settingsService.GetSnowflakeAsync(
                guildId, GuildFeature.RoeViolationReports, GuildAudience.Alliance, guildAllianceId, RoeViolationReportsSettingKeys.Channel);
        if (channelIdResult is not { } channelId)
            return await ResultEmbedAsync(guildId, Msg.Roe.Title(lang), Msg.Roe.ChannelNotConfigured(lang));

        // Mentioned inline in the instructions so the reporter knows who picks the case up; the
        // same per-alliance Diplomat role that SetReadyForDiplomatAsync pings later.
        var diplomatRoleId = guildAllianceId is null
            ? null
            : await settingsService.GetSnowflakeAsync(
                guildId, GuildFeature.Diplomacy, GuildAudience.Alliance, guildAllianceId, DiplomacySettingKeys.DiplomatRole);
        var diplomatMention = diplomatRoleId is { } diplomatId ? $"<@&{diplomatId}>" : Msg.Roe.DiplomatFallback(lang);

        var report = new RoeViolationReport
        {
            GuildId = guildId,
            GuildAllianceId = guildAllianceId,
            AttackerAllianceTag = attackerTag,
            AttackerCommanderName = attackerName,
            DefenderAllianceTag = defenderTag,
            DefenderCommanderName = defenderName,
            ReportedByDiscordUserId = reporterId,
            Status = RoeViolationStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.RoeViolationReports.Add(report);
        await db.SaveChangesAsync();

        var threadName = $"{report.Id}-{Slugify(attackerTag)}-vs-{Slugify(defenderTag)}";
        if (threadName.Length > 100)
            threadName = threadName[..100];

        // A forum post's starter message is required at creation time — unlike a normal text
        // channel's thread (create empty, send the first message after), Discord's forum-thread
        // endpoint has no "create, then fill in" step, so the embed has to be built first.
        var embed = await embedBranding.BuildBrandedAsync(guildId,
            BuildInstructions(lang, reporterDisplayName, reporterIsVictim, diplomatMention),
            title: $"[{attackerTag}] {attackerName} - [{defenderTag}] {defenderName}");

        // Ping the reporter (and the reported own player, if known) in the starter message so
        // they're pulled into the forum post and notified — same as pinging them by hand.
        var mentions = new List<string> { $"<@{reporterId}>" };
        if (attackerDiscordUserId is { } mentionedAttackerId && mentionedAttackerId != reporterId)
            mentions.Add($"<@{mentionedAttackerId}>");

        ForumGuildThread thread;
        try
        {
            thread = await gatewayClient.Rest.CreateForumGuildThreadAsync(channelId, new ForumGuildThreadProperties(threadName,
                new ForumGuildThreadMessageProperties
                {
                    Content = string.Join(' ', mentions),
                    Embeds = [embed],
                    Components = [new ActionRowProperties([ReadyButton(report.Id, lang), DoneButton(report.Id, lang)])],
                }));
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            db.RoeViolationReports.Remove(report);
            await db.SaveChangesAsync();
            var guildLang = await languageResolver.ForGuildAsync(guildId);
            // Creating a forum post is Send Messages ("Create Posts" in a forum's own permission
            // UI), not Create Public Threads — see ChannelAccessProfile.ForumPosts.
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.CreateRoeForumPost, channelId,
                BotPermission.ViewChannel | BotPermission.SendMessages | BotPermission.EmbedLinks);
            return await ResultEmbedAsync(guildId, Msg.Roe.Title(lang), Msg.Roe.Misconfigured(lang));
        }

        report.ThreadId = thread.Id;
        await db.SaveChangesAsync();

        try
        {
            await gatewayClient.Rest.AddGuildThreadUserAsync(thread.Id, reporterId);
            if (attackerDiscordUserId is { } attackerId && attackerId != reporterId)
                await gatewayClient.Rest.AddGuildThreadUserAsync(thread.Id, attackerId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            // The post exists even if this part fails — nothing to roll back, just report it.
            var guildLang = await languageResolver.ForGuildAsync(guildId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.AddRoeThreadUsers, thread.Id, BotPermission.SendMessagesInThreads);
        }

        return await ResultEmbedAsync(guildId, Msg.Roe.CreatedTitle(lang),
            Msg.Roe.CreatedBody(lang, reporterDisplayName, $"<#{thread.Id}>"));
    }

    public async Task<string> SetReadyForDiplomatAsync(int reportId, ulong callerId)
    {
        var report = await db.RoeViolationReports.FindAsync(reportId);
        if (report is null)
            return Msg.Roe.ReportNotFound(await languageResolver.ForUserAsync(callerId));

        // Status strings go back ephemerally to whoever clicked; the thread message addresses
        // the reporter the forum post is dedicated to (the same person once the guard passes,
        // but each resolved per its own rule).
        var callerLang = await languageResolver.ForUserAsync(callerId, scopeGuildId: report.GuildId);
        if (callerId != report.ReportedByDiscordUserId)
            return Msg.Roe.OnlyReporterConfirm(callerLang);

        // The diplomat pinged is the one for the report's own alliance (fallback to primary for
        // legacy reports created before per-alliance scoping).
        var diplomacyAllianceId = report.GuildAllianceId ?? await allianceService.GetPrimaryIdAsync(report.GuildId);
        var diplomatRoleId = diplomacyAllianceId is null
            ? null
            : await settingsService.GetSnowflakeAsync(
                report.GuildId, GuildFeature.Diplomacy, GuildAudience.Alliance, diplomacyAllianceId, DiplomacySettingKeys.DiplomatRole);
        var mention = diplomatRoleId is { } roleId ? $"<@&{roleId}>" : null;

        var reporterLang = await languageResolver.ForUserAsync(report.ReportedByDiscordUserId, scopeGuildId: report.GuildId);
        try
        {
            var embed = await embedBranding.BuildBrandedAsync(report.GuildId, Msg.Roe.CaseReady(reporterLang));
            await gatewayClient.Rest.SendMessageAsync(report.ThreadId,
                new MessageProperties { Content = mention, Embeds = [embed] });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            var guildLang = await languageResolver.ForGuildAsync(report.GuildId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(report.GuildId, BotAction.SendRoeThreadMessage, report.ThreadId,
                BotPermission.SendMessagesInThreads | BotPermission.EmbedLinks);
            return Msg.Roe.SendFailed(callerLang);
        }

        return Msg.Roe.DiplomatNotified(callerLang);
    }

    public async Task<string> CloseReportAsync(int reportId, ulong callerId)
    {
        var report = await db.RoeViolationReports.FindAsync(reportId);
        if (report is null)
            return Msg.Roe.ReportNotFound(await languageResolver.ForUserAsync(callerId));

        // Everything rendered here is the ephemeral status back to the clicking user —
        // archiving the thread posts nothing into it.
        var callerLang = await languageResolver.ForUserAsync(callerId, scopeGuildId: report.GuildId);
        if (callerId != report.ReportedByDiscordUserId)
            return Msg.Roe.OnlyReporterClose(callerLang);
        if (report.Status == RoeViolationStatus.Closed)
            return Msg.Roe.AlreadyClosed(callerLang);

        try
        {
            await gatewayClient.Rest.ModifyGuildChannelAsync(report.ThreadId, o =>
            {
                o.Archived = true;
                o.Locked = true;
            });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            var guildLang = await languageResolver.ForGuildAsync(report.GuildId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(report.GuildId, BotAction.CloseRoeThread, report.ThreadId, BotPermission.ManageThreads);
            return Msg.Roe.CloseFailed(callerLang);
        }

        report.Status = RoeViolationStatus.Closed;
        report.ClosedAt = DateTimeOffset.UtcNow;
        report.ClosedByDiscordUserId = callerId;
        await db.SaveChangesAsync();

        return Msg.Roe.Closed(callerLang);
    }

    private static string Slugify(string value)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }

        return sb.ToString().Trim('-');
    }
}
