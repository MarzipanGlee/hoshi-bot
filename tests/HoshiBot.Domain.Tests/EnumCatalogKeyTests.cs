using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Xunit;

namespace HoshiBot.Domain.Tests;

// Enum-keyed catalog families ("Notify.Action.<BotAction>", "Perm.<BotPermission>") are the one
// place the other catalog tests cannot help. MessageCatalogKeyUsageTests only sees literal keys, so
// it never learns these exist; the locale-parity tests compare the files against each other, so a
// value missing from both passes. And both accessors fall back to the enum name rather than
// throwing — deliberately, so a gap degrades instead of leaking a raw key, but that also means a
// gap is invisible unless something asserts it.
//
// These reports go to a guild's admin channel in that guild's language, so "ManageNicknames"
// leaking into a German embed is exactly the sort of thing nobody notices for months.
//
// Asserted against the catalog itself rather than through the accessors: "Administrator" is its own
// English label, so an accessor-based check cannot tell a defined key from a fallback.
public class EnumCatalogKeyTests
{
    public static TheoryData<Language> EnabledLanguages() => [.. Languages.Enabled];

    [Theory]
    [MemberData(nameof(EnabledLanguages))]
    public void Every_BotAction_has_a_localized_label(Language language)
    {
        var templates = MessageCatalog.Templates(language);
        foreach (var action in Enum.GetValues<BotAction>())
        {
            Assert.True(templates.ContainsKey($"Notify.Action.{action}"),
                $"{Languages.ToCode(language)}.json is missing 'Notify.Action.{action}' — the report would show the raw enum name.");
        }
    }

    [Theory]
    [MemberData(nameof(EnabledLanguages))]
    public void Every_BotPermission_has_a_localized_name(Language language)
    {
        var templates = MessageCatalog.Templates(language);
        foreach (var permission in Enum.GetValues<BotPermission>())
        {
            if (permission == BotPermission.None)
                continue;

            Assert.True(templates.ContainsKey($"Perm.{permission}"),
                $"{Languages.ToCode(language)}.json is missing 'Perm.{permission}' — the report would show the raw enum name.");
        }
    }

    // A [Flags] value normally carries several bits at once here, and the report has to name all of
    // them: "missing Create Posts" when Send Messages in Posts is also missing would send an admin
    // back a second time.
    [Fact]
    public void Perm_List_names_every_set_bit()
    {
        var list = Msg.Perm.List(Language.En, BotPermission.SendMessages | BotPermission.SendMessagesInThreads);

        Assert.Contains(Msg.Perm.Name(Language.En, BotPermission.SendMessages), list);
        Assert.Contains(Msg.Perm.Name(Language.En, BotPermission.SendMessagesInThreads), list);
        Assert.DoesNotContain(Msg.Perm.Name(Language.En, BotPermission.ManageThreads), list);
    }
}
