using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Discord.MemberOnboarding;

// The opt-in, member-facing half of automated player assignment (the MemberOnboarding feature): DMs a
// member whose nickname the PlayerLink matcher couldn't resolve, asking them to confirm the bot's
// best guess or type their in-game name, then creates the UserPlayer link and marks their
// PlayerLinkReview row Resolved. Purely button/modal-driven — no free-text DM parsing, so it never
// collides with the member-lore interview's DM router. Creating the link makes every role-sync job
// pick the member up automatically; this service grants no roles itself.
public class MemberOnboardingService(
    HoshiBotDbContext db,
    NotificationDispatcher notificationDispatcher,
    PlayerLinkService playerLinkService,
    ILogger<MemberOnboardingService> logger)
{
    // Custom-ID prefixes; NetCord parses the ":"-separated tail into the handler method's parameters.
    public const string ConfirmButtonId = "player-link-confirm";      // player-link-confirm:{reviewId}:{playerId}
    public const string NameButtonId = "player-link-name";            // player-link-name:{reviewId}
    public const string NameModalId = "player-link-name-modal";       // player-link-name-modal:{reviewId}
    public const string NameInputId = "ingame-name";

    private const string LinkedTemplate =
        "✅ Super, ich habe dich mit **{0}** verknüpft. Deine Rollen werden in Kürze automatisch gesetzt. 🖖";

    // Sends the outreach DM for one Unresolved review and records the result on the row. Called by the
    // MemberOnboardingSyncJob (which shares this scope's DbContext, so the row it passes is tracked here).
    public async Task<bool> SendOutreachAsync(PlayerLinkReview review, CancellationToken cancellationToken)
    {
        // A global UserPlayer link may have appeared since this row was created (the member self-linked,
        // an admin assigned them, or they were linked in another shared guild) — never DM in that case.
        if (await db.UserPlayers.AnyAsync(up => up.DiscordUserId == review.DiscordUserId, cancellationToken))
        {
            review.Status = PlayerLinkReviewStatus.Resolved;
            review.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        var candidates = await playerLinkService.ResolveCandidatesAsync(review.GuildId, review.Nickname);
        var best = review.CandidateStfcPlayerId is { } cid
            ? candidates.FirstOrDefault(p => p.Id == cid)
            : candidates.Count == 1 ? candidates[0] : null;

        string content;
        List<ActionRowProperties> rows;
        if (best is not null)
        {
            content =
                "🖖 Hi! Damit ich dir automatisch die richtigen Rollen geben kann, würde ich dich gern deinem " +
                $"Spieler zuordnen. Ich glaube, du bist **{best.Name}**. Stimmt das?";
            rows =
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"{ConfirmButtonId}:{review.Id}:{best.Id}", "Ja, das bin ich ✅", ButtonStyle.Success),
                    new ButtonProperties($"{NameButtonId}:{review.Id}", "Nein, anderer Spieler ✏️", ButtonStyle.Secondary),
                ]),
            ];
        }
        else
        {
            content =
                "🖖 Hi! Damit ich dir automatisch die richtigen Rollen geben kann, würde ich dich gern deinem " +
                "Spieler zuordnen. Wie heißt du im Spiel?";
            rows =
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"{NameButtonId}:{review.Id}", "Namen eingeben ✏️", ButtonStyle.Primary),
                ]),
            ];
        }

        var messageId = await notificationDispatcher.SendDirectMessageAsync(review.DiscordUserId, content, rows);
        review.Status = messageId is null ? PlayerLinkReviewStatus.Undeliverable : PlayerLinkReviewStatus.DmSent;
        review.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (messageId is null)
            logger.LogInformation("MemberOnboarding: DMs closed for user {UserId} (review {ReviewId}) — marked undeliverable.",
                review.DiscordUserId, review.Id);
        return messageId is not null;
    }

    // The member confirmed the bot's guess → link + resolve. Returns the DM reply text.
    public async Task<string> ConfirmAsync(int reviewId, int playerId, ulong userId, CancellationToken cancellationToken)
    {
        var player = await db.StfcPlayers.FindAsync(playerId);
        if (player is null)
            return "Hoppla — diesen Spieler gibt es nicht mehr. Bitte wende dich an einen Admin.";

        await playerLinkService.LinkAsync(userId, playerId);
        await playerLinkService.MarkUserResolvedAsync(userId);
        return string.Format(LinkedTemplate, player.Name);
    }

    // The member typed an in-game name → resolve it against the catalog and link if unique.
    public async Task<string> ResolveByNameAsync(int reviewId, ulong userId, string typedName, CancellationToken cancellationToken)
    {
        var review = await db.PlayerLinkReviews.FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);
        if (review is null)
            return "Diese Anfrage ist nicht mehr aktuell.";

        var name = typedName.Trim();
        var candidates = await playerLinkService.ResolveCandidatesAsync(review.GuildId, name);
        if (candidates.Count == 0)
            return $"Ich konnte keinen Spieler namens **{name}** finden. " +
                   "Bitte prüfe die Schreibweise oder wende dich an einen Admin.";
        if (candidates.Count > 1)
            return $"Es gibt mehrere Spieler namens **{name}** — bitte wende dich an einen Admin, damit er dich richtig zuordnet.";

        await playerLinkService.LinkAsync(userId, candidates[0].Id);
        await playerLinkService.MarkUserResolvedAsync(userId);
        return string.Format(LinkedTemplate, candidates[0].Name);
    }
}
