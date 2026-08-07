using HoshiBot.Data;
using HoshiBot.Domain.Localization;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.ReadReceipts;

// The ✅ under any tracked post. Kind-agnostic on purpose: an announcement, a forwarded translation
// and (later) the alliance rules all carry the same button and land here.
public class ReadReceiptButtonModule(ReadReceiptService readReceipts, EmbedBranding embedBranding, LanguageResolver languageResolver)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("read-receipt")]
    public Task MarkRead(int postId) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var lang = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

            // The row is gone — the post outlived its record, or a button survived a database
            // restore. Say so rather than recording a receipt against nothing.
            if (await readReceipts.FindAsync(postId) is not { } post)
                return Msg.Announce.ReadPostGone(lang);

            var (wasNew, count) = await readReceipts.MarkReadAsync(postId, Context.Guild!.Id, Context.User.Id);

            try
            {
                // The public post's own count, edited separately from this personal ephemeral ack —
                // and in the post's language, not the clicking member's.
                await Context.Client.Rest.ModifyMessageAsync(Context.Channel.Id, Context.Message.Id,
                    m => m.Components = [ReadReceiptService.Buttons(postId, count, post.Language)]);
            }
            catch (RestException)
            {
                // AnnouncementCounterRefreshJob picks this up if the inline edit fails (a transient
                // rate limit, say) — not worth failing the member's interaction over.
            }

            return wasNew ? Msg.Announce.ReadRecorded(lang) : Msg.Announce.AlreadyRead(lang);
        });
}
