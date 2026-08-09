using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;

namespace HoshiBot.Discord.ReadReceipts;

// What a feature wants to happen when someone confirms one of ITS posts, beyond recording the
// receipt. Boarding is the first: confirming the welcome message is what grants the member role.
//
// An extension point rather than a branch in ReadReceiptButtonModule, because that module's whole
// design is to be kind-agnostic — an announcement, a translation and a welcome message all carry the
// same button and land in the same handler. A feature registers its own follow-up and keeps its
// knowledge inside its own folder; ReadReceipts keeps knowing nothing about any of them.
public interface IReadConfirmationFollowUp
{
    ReadablePostKind Kind { get; }

    // Runs after the receipt is recorded. Returns the text to show the member instead of the generic
    // "recorded"/"already read", or null to keep the generic one.
    //
    // Called on EVERY confirmation, including one where the receipt already existed. A follow-up
    // must be idempotent and must decide from real state — if a first click granted a role and then
    // failed halfway, the second click is the only chance to finish, and it arrives with the receipt
    // already in place.
    Task<string?> OnConfirmedAsync(ReadablePost post, GuildUser member, Language lang);
}
