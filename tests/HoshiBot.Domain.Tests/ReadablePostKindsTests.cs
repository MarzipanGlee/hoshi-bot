using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Tests;

public class ReadablePostKindsTests
{
    // The editor shows every kind but only lets the implemented ones be switched on. If a kind gets
    // a producer and nobody moves it into Implemented, its checkbox stays disabled and the feature
    // silently never applies to it.
    [Fact]
    public void Implemented_kinds_are_the_ones_with_a_producer()
    {
        Assert.Equal(
            [ReadablePostKind.Announcement, ReadablePostKind.ForwardedAnnouncement, ReadablePostKind.WelcomeMessage],
            ReadablePostKinds.Implemented);
    }

    [Fact]
    public void Unimplemented_kinds_are_reported_as_such()
    {
        Assert.False(ReadablePostKinds.IsImplemented(ReadablePostKind.DiplomacyPost));
        Assert.True(ReadablePostKinds.IsImplemented(ReadablePostKind.WelcomeMessage));   // Boarding produces it
        Assert.False(ReadablePostKinds.IsImplemented(ReadablePostKind.AllianceRules));
    }

    // Every kind needs a distinct setting key: a collision would make two kinds share one switch.
    [Fact]
    public void Every_kind_has_its_own_setting_key()
    {
        var keys = Enum.GetValues<ReadablePostKind>().Select(ReadReceiptsSettingKeys.ForKind).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Equal("Kind.Announcement", ReadReceiptsSettingKeys.ForKind(ReadablePostKind.Announcement));
    }

    // The ordinal is stored on every ReadablePost row, so reordering the enum would silently
    // relabel existing posts.
    [Fact]
    public void Kind_ordinals_are_pinned()
    {
        Assert.Equal(0, (int)ReadablePostKind.Announcement);
        Assert.Equal(1, (int)ReadablePostKind.ForwardedAnnouncement);
        Assert.Equal(2, (int)ReadablePostKind.DiplomacyPost);
        Assert.Equal(3, (int)ReadablePostKind.WelcomeMessage);
        Assert.Equal(4, (int)ReadablePostKind.AllianceRules);
    }
}
