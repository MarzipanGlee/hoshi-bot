using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

// The one place NetCord is allowed inside this project (see the csproj comment). BotPermission
// exists so HoshiBot.Domain can declare permissions without referencing NetCord, and every consumer
// converts with the plain cast (Permissions)(ulong)value — which is only correct as long as the two
// enums agree bit for bit. That agreement is not enforced by anything except this file.
public class BotPermissionTests
{
    [Fact]
    public void Values_match_NetCord_by_name()
    {
        foreach (var name in Enum.GetNames<BotPermission>())
        {
            if (name == nameof(BotPermission.None))
                continue;

            Assert.True(Enum.TryParse<NetCord.Permissions>(name, out var netCord),
                $"NetCord.Permissions has no member named '{name}' — it was renamed, or this one is a typo.");
            Assert.Equal((ulong)netCord, (ulong)Enum.Parse<BotPermission>(name));
        }
    }

    // Two bits that have been quietly split out of broader ones by Discord and cost us a 403 each
    // time. Pinned explicitly so a future "tidy up the enum" can't silently fold them back in.
    [Fact]
    public void Split_out_bits_are_distinct_from_their_former_parents()
    {
        Assert.NotEqual(BotPermission.ManageMessages, BotPermission.PinMessages);
        Assert.NotEqual(BotPermission.SendMessages, BotPermission.SendMessagesInThreads);
        Assert.Equal(BotPermission.None, BotPermission.ManageMessages & BotPermission.PinMessages);
        Assert.Equal(BotPermission.None, BotPermission.SendMessages & BotPermission.SendMessagesInThreads);
    }
}
